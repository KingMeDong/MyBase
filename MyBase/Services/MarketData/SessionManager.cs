using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyBase.Data;
using MyBase.Models.Finance;

namespace MyBase.Services.MarketData;

/// <summary>
/// Kümmert sich um die Verbindung zum IB Client Portal Gateway.
/// Prüft Auth-Status, sendet Heartbeats (/tickle),
/// und aktualisiert FeedState in der Datenbank.
/// </summary>
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
        IOptionsSnapshot<CpapiOptions> opt) {
        _httpFactory = httpFactory;
        _sp = sp;
        _log = log;

        _heartbeat = TimeSpan.FromSeconds(opt.Value.HeartbeatSeconds);
        _statusPoll = TimeSpan.FromSeconds(opt.Value.StatusPollSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var client = _httpFactory.CreateClient("Cpapi");
        var lastTickle = DateTime.MinValue;
        var lastStatus = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested) {
            try {
                // 1) DesiredState aus DB lesen
                var desired = await GetDesiredStateAsync(stoppingToken);

                // 2) Status prüfen (seltener)
                if ((DateTime.UtcNow - lastStatus) > _statusPoll) {
                    _state = await ProbeAuthAsync(client, stoppingToken);
                    lastStatus = DateTime.UtcNow;
                }

                // 3) Heartbeat schicken (häufiger)
                if ((DateTime.UtcNow - lastTickle) > _heartbeat &&
                    (desired == "Running" || _state == SessionState.Connected)) {
                    await TickleAsync(client, stoppingToken);
                    lastTickle = DateTime.UtcNow;
                    await UpdateHeartbeatAsync();
                }

                // 4) Falls auth ok aber noch nicht connected: Brokerage Session aktivieren
                if (_state == SessionState.Connecting) {
                    await EnsureConnectedAsync(client, stoppingToken);
                }

                await Task.Delay(1000, stoppingToken); // kleine Pause im Loop
            } catch (OperationCanceledException) { /* Shutdown */ } catch (Exception ex) {
                _log.LogError(ex, "Fehler im SessionManager");
                _state = SessionState.Error;
                await SetFeedLastErrorAsync(ex.Message);
                await Task.Delay(3000, stoppingToken); // Backoff
            }
        }
    }
    private async Task<string> GetDesiredStateAsync(CancellationToken ct) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.AppSettings.FindAsync(new object?[] { "MarketDataDesiredState" }, ct);
        return s?.Value ?? "Stopped";
    }

    private async Task<SessionState> ProbeAuthAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/iserver/auth/status", ct);
            if (!res.IsSuccessStatusCode) return SessionState.Disconnected;

            var json = await res.Content.ReadFromJsonAsync<AuthStatus>(cancellationToken: ct);
            if (json is null) return SessionState.Disconnected;

            if (!json.authenticated) return SessionState.NeedsLogin;
            if (json.authenticated && !json.connected) return SessionState.Connecting;
            if (json.authenticated && json.connected) return SessionState.Connected;

            return SessionState.Disconnected;
        } catch {
            return SessionState.Disconnected;
        }
    }

    private async Task TickleAsync(HttpClient c, CancellationToken ct) {
        try {
            await c.GetAsync("/v1/api/tickle", ct);
        } catch {
            // Fehler beim Heartbeat ignorieren, Loop probiert es erneut
        }
    }

    private async Task EnsureConnectedAsync(HttpClient c, CancellationToken ct) {
        try {
            var res = await c.GetAsync("/v1/api/iserver/accounts", ct);
            if (res.IsSuccessStatusCode) {
                _state = SessionState.Connected;
            }
        } catch {
            // bleibt auf Connecting, bis es klappt
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

    // --- Hilfsfunktionen folgen ---
}
