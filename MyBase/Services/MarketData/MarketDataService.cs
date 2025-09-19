using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models.Finance;

namespace MyBase.Services.MarketData;

/// <summary>
/// Steuert den Marktdaten-Feed abhängig von DesiredState (AppSetting)
/// und dem Session-Status (SessionManager). In diesem Schritt wird nur
/// der Status in FeedState gepflegt (noch keine Kerzen/Subscriptions).
/// </summary>
public class MarketDataService : BackgroundService {
    private readonly IServiceProvider _sp;
    private readonly ILogger<MarketDataService> _log;
    private readonly SessionManager _session;

    // interner Marker, ob der Feed "läuft"
    private bool _isRunning;

    public MarketDataService(IServiceProvider sp, ILogger<MarketDataService> log, SessionManager session) {
        _sp = sp;
        _log = log;
        _session = session;
    }

    protected override async Task ExecuteAsync(CancellationToken stop) {
        while (!stop.IsCancellationRequested) {
            try {
                var desired = await GetDesiredAsync(stop);

                // Start-Bedingung: gewünschter Zustand Running + Session verbunden
                if (desired == "Running" && _session.State == SessionState.Connected) {
                    if (!_isRunning) {
                        await OnStartAsync();     // (hier später IB-Subscriptions öffnen)
                        await SetFeedStateAsync(FeedRunState.Running, null);
                        _isRunning = true;
                    }
                } else {
                    if (_isRunning) {
                        await OnStopAsync();      // (hier später IB-Subscriptions schließen)
                        await SetFeedStateAsync(FeedRunState.Stopped, null);
                        _isRunning = false;
                    } else {
                        // Session-Fehler spiegeln
                        if (_session.State == SessionState.Error)
                            await SetFeedStateAsync(FeedRunState.Error, "Session error");
                        else if (desired == "Stopped")
                            await SetFeedStateAsync(FeedRunState.Stopped, null);
                    }
                }

                await Task.Delay(1000, stop); // kleines, enges Polling
            } catch (OperationCanceledException) { /* normal beim Shutdown */ } catch (Exception ex) {
                _log.LogError(ex, "MarketDataService Fehler");
                await SetFeedStateAsync(FeedRunState.Error, ex.Message);
                await Task.Delay(3000, stop); // kurzer Backoff
            }
        }

        // sauberer Shutdown
        if (_isRunning) {
            await OnStopAsync();
            await SetFeedStateAsync(FeedRunState.Stopped, null);
            _isRunning = false;
        }
    }

    // -------------------------------------------------
    // Hilfsfunktionen
    // -------------------------------------------------

    private async Task<string> GetDesiredAsync(CancellationToken ct) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.AppSettings.FindAsync(new object?[] { "MarketDataDesiredState" }, ct);
        return s?.Value ?? "Stopped";
    }

    private async Task SetFeedStateAsync(FeedRunState state, string? error) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Schritt 1: erstes aktives Instrument (z. B. SPY)
        var inst = await db.Instruments.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync();

        if (inst is null) return;

        var fs = await db.FeedStates.FindAsync(inst.Id) ?? new FeedState { InstrumentId = inst.Id };
        fs.Status = (byte)state;
        fs.LastError = error;

        db.Update(fs);
        await db.SaveChangesAsync();
    }

    // Hooks: in Schritt 2 öffnen/schließen wir hier echte Subscriptions
    private Task OnStartAsync() {
        _log.LogInformation("MarketDataService starting (placeholder) — hier später IB-Subscriptions öffnen.");
        return Task.CompletedTask;
    }

    private Task OnStopAsync() {
        _log.LogInformation("MarketDataService stopping (placeholder) — hier später IB-Subscriptions schließen.");
        return Task.CompletedTask;
    }
}
