using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyBase.Data;
using MyBase.Models.Finance;

namespace MyBase.Services.MarketData;

public class SessionManager : BackgroundService {
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceProvider _sp;
    private readonly ILogger<SessionManager> _log;
    private readonly TimeSpan _heartbeat;
    private readonly TimeSpan _statusPoll;

    private SessionState _state = SessionState.Disconnected;
    public SessionState State => _state;

    public SessionManager(
        IHttpClientFactory httpFactory,
        IServiceProvider sp,
        ILogger<SessionManager> log,
        IOptionsMonitor<CpapiOptions> opt
    ) {
        _httpFactory = httpFactory;
        _sp = sp;
        _log = log;

        var cfg = opt.CurrentValue;
        _heartbeat = TimeSpan.FromSeconds(cfg.HeartbeatSeconds);
        _statusPoll = TimeSpan.FromSeconds(cfg.StatusPollSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var client = _httpFactory.CreateClient("Cpapi");
        var lastTickle = DateTime.MinValue;
        var lastStatus = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var gwDesired = await GetGatewayDesiredAsync(stoppingToken);

                // ⬇️ Wenn Gateway gestoppt ist, nichts tun
                if (gwDesired == "Stopped") {
                    _state = SessionState.Disconnected;
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                var feedDesired = await GetFeedDesiredAsync(stoppingToken);

                // 🔹 SSO validieren
                var ssoOk = await EnsureSsoAsync(client, stoppingToken);
                if (!ssoOk) {
                    _state = SessionState.NeedsLogin;
                } else {
                    var fastProbe = _state != SessionState.Connected;
                    var due = fastProbe ? TimeSpan.FromSeconds(5) : _statusPoll;

                    if ((DateTime.UtcNow - lastStatus) > due) {
                        _state = await ProbeAuthAsync(client, stoppingToken);
                        lastStatus = DateTime.UtcNow;

                        if (_state == SessionState.Connecting)
                            await EnsureConnectedAsync(client, stoppingToken);
                    }
                }

                // Heartbeat senden (immer wenn Session aktiv läuft)
                if ((DateTime.UtcNow - lastTickle) > _heartbeat &&
                    _state == SessionState.Connected) {
                    await TickleAsync(client, stoppingToken);
                    lastTickle = DateTime.UtcNow;
                    await UpdateHeartbeatAsync();
                }

                await Task.Delay(1000, stoppingToken);
            } catch (OperationCanceledException) { } catch (Exception ex) {
                _log.LogError(ex, "Fehler im SessionManager");
                _state = SessionState.Error;
                await SetFeedLastErrorAsync(ex.Message);
                SessionLogBuffer.Append($"Exception: {ex.Message}");
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    private async Task<string> GetGatewayDesiredAsync(CancellationToken ct) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.AppSettings.FindAsync(new object?[] { "GatewayDesiredState" }, ct);
        return s?.Value ?? "Running";
    }

    private async Task<string> GetFeedDesiredAsync(CancellationToken ct) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.AppSettings.FindAsync(new object?[] { "MarketDataDesiredState" }, ct);
        return s?.Value ?? "Stopped";
    }

    private async Task<bool> EnsureSsoAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/sso/validate", ct);
            if (res.IsSuccessStatusCode) {
                SessionLogBuffer.Append("SSO validate: ok");
                return true;
            }

            SessionLogBuffer.Append($"SSO validate: {(int)res.StatusCode}");
            return false;
        } catch (Exception ex) {
            SessionLogBuffer.Append($"SSO validate Fehler: {ex.Message}");
            return false;
        }
    }

    private async Task<SessionState> ProbeAuthAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/iserver/auth/status", ct);

            if (!res.IsSuccessStatusCode) {
                SessionLogBuffer.Append($"Auth status HTTP {(int)res.StatusCode}");
                return SessionState.Disconnected;
            }

            var json = await res.Content.ReadFromJsonAsync<AuthStatus>(cancellationToken: ct);
            if (json is null) {
                SessionLogBuffer.Append("Auth status: leer");
                return SessionState.Disconnected;
            }

            SessionLogBuffer.Append($"Auth status: authenticated={json.authenticated}, connected={json.connected}");

            if (!json.authenticated) return SessionState.NeedsLogin;
            if (json.authenticated && !json.connected) return SessionState.Connecting;
            if (json.authenticated && json.connected) return SessionState.Connected;

            return SessionState.Disconnected;
        } catch (Exception ex) {
            SessionLogBuffer.Append($"Auth status Fehler: {ex.Message}");
            return SessionState.Disconnected;
        }
    }

    private async Task TickleAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/tickle", ct);
            SessionLogBuffer.Append($"Tickle: {(int)res.StatusCode}");
        } catch (Exception ex) {
            SessionLogBuffer.Append($"Tickle Fehler: {ex.Message}");
        }
    }

    private async Task EnsureConnectedAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/iserver/accounts", ct);
            if (res.IsSuccessStatusCode) {
                _state = SessionState.Connected;
                SessionLogBuffer.Append("Accounts call → Connected");
            } else {
                SessionLogBuffer.Append($"Accounts call: {(int)res.StatusCode}");
            }
        } catch (Exception ex) {
            SessionLogBuffer.Append($"Accounts Fehler: {ex.Message}");
        }
    }

    private async Task UpdateHeartbeatAsync() {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inst = await db.Instruments.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync();

        if (inst is null) return;

        var fs = await db.FeedStates.FindAsync(inst.Id) ?? new FeedState { InstrumentId = inst.Id };
        fs.LastHeartbeatUtc = DateTime.UtcNow;

        db.Update(fs);
        await db.SaveChangesAsync();
    }

    private async Task SetFeedLastErrorAsync(string msg) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inst = await db.Instruments.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync();

        if (inst is null) return;

        var fs = await db.FeedStates.FindAsync(inst.Id) ?? new FeedState { InstrumentId = inst.Id };
        fs.Status = (byte)FeedRunState.Error;
        fs.LastError = msg;

        db.Update(fs);
        await db.SaveChangesAsync();
    }

    private sealed class AuthStatus {
        public bool authenticated { get; set; }
        public bool connected { get; set; }
    }
}
