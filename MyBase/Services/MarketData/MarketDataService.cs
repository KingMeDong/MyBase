using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models.Finance;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyBase.Services.MarketData;

/// <summary>
/// Steuert den Marktdaten-Feed abhängig von DesiredState (AppSetting)
/// und dem Session-Status (SessionManager). In dieser Version:
/// Snapshot-Polling -> MinuteBarBuilder -> Bar_1m speichern.
/// </summary>
public class MarketDataService : BackgroundService {
    private readonly IServiceProvider _sp;
    private readonly ILogger<MarketDataService> _log;
    private readonly SessionManager _session;
    private readonly IHttpClientFactory _httpFactory;

    private bool _isRunning;
    private Task? _runner;
    private CancellationTokenSource? _cts;

    public MarketDataService(
        IServiceProvider sp,
        ILogger<MarketDataService> log,
        SessionManager session,
        IHttpClientFactory httpFactory) {
        _sp = sp;
        _log = log;
        _session = session;
        _httpFactory = httpFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stop) {
        while (!stop.IsCancellationRequested) {
            try {
                var desired = await GetDesiredAsync(stop);

                // Startbedingung: gewünschter Zustand Running + Session verbunden
                if (desired == "Running" && _session.State == SessionState.Connected) {
                    if (!_isRunning) {
                        _cts = new CancellationTokenSource();
                        _runner = Task.Run(() => RunAsync(_cts.Token));
                        await SetFeedStateAsync(FeedRunState.Running, null);
                        _isRunning = true;
                    }
                } else {
                    if (_isRunning) {
                        _cts?.Cancel();
                        try { if (_runner != null) await _runner; } catch { /* ignore */ }
                        _isRunning = false;
                        await SetFeedStateAsync(FeedRunState.Stopped, null);
                    }
                }

                await Task.Delay(1000, stop);
            } catch (OperationCanceledException) { /* normal */ } catch (Exception ex) {
                _log.LogError(ex, "MarketDataService Fehler");
                await SetFeedStateAsync(FeedRunState.Error, ex.Message);
                await Task.Delay(3000, stop);
            }
        }

        // sauberer Shutdown
        if (_isRunning) {
            _cts?.Cancel();
            try { if (_runner != null) await _runner; } catch { }
            _isRunning = false;
            await SetFeedStateAsync(FeedRunState.Stopped, null);
        }
    }
    private static long? TryGetLong(JsonElement obj, string key) {
        if (!obj.TryGetProperty(key, out var v)) return null;

        if (v.ValueKind == JsonValueKind.Number) {
            if (v.TryGetInt64(out var l)) return l;
            if (v.TryGetDecimal(out var d)) return (long)d;
        }
        if (v.ValueKind == JsonValueKind.String &&
            long.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                          System.Globalization.CultureInfo.InvariantCulture, out var ls))
            return ls;

        return null;
    }

    // -------------------------------------------------
    // Feed-Runner: Snapshot-Polling -> MinuteBarBuilder
    // -------------------------------------------------
    private async Task RunAsync(CancellationToken ct) {
        var http = _httpFactory.CreateClient("Cpapi");
        var builder = new MinuteBarBuilder();

        // Instrument abrufen (erstes aktives Instrument – bei dir SPY)
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inst = await db.Instruments.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Id)
            .FirstAsync(ct);

        var conid = inst.IbConId; // z. B. 756733 für SPY
        SessionLogBuffer.Append($"Feed: starte Snapshot-Poll für {inst.Symbol} (conid={conid})");

        long lastTotalVol = -1;

        while (!ct.IsCancellationRequested) {
            try {
                // Snapshot: 31 = last price, 88 = total volume
                var url = $"/v1/api/iserver/marketdata/snapshot?conids={conid}&fields=31,88";
                var res = await http.GetAsync(url, ct);
                if (!res.IsSuccessStatusCode) {
                    SessionLogBuffer.Append($"snapshot {(int)res.StatusCode}");
                    await Task.Delay(1000, ct);
                    continue;
                }

                var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

                // Antwort ist i. d. R. ein Array mit einem Objekt je conid
                if (json.ValueKind == JsonValueKind.Array && json.GetArrayLength() > 0) {
                    var obj = json[0];

                    // Preis: versuche 31 (last), fallback 84/85 (bid/ask mid je nach Gateway-Version)
                    var last = TryGetDecimal(obj, "31") ?? TryGetDecimal(obj, "84") ?? TryGetDecimal(obj, "85");
                    long? totalVol = TryGetLong(obj, "88");
                    if (last.HasValue) {
                        var nowUtc = DateTime.UtcNow;
                        var finished = builder.PushTick(nowUtc, last.Value, totalVol ?? -1L); // <-- -1L (long)
                        if (finished is not null) {
                            var (minuteUtc, O, H, L, C, V) = finished.Value;
                            await PersistBarAsync(inst.Id, minuteUtc, O, H, L, C, V);
                            SessionLogBuffer.Append($"Bar 1m @ {minuteUtc:HH:mm}  O={O} H={H} L={L} C={C} V={V}");
                        }
                    }

                    if (totalVol.HasValue) lastTotalVol = totalVol.Value;

                }

                await Task.Delay(1000, ct); // Poll-Intervall
            } catch (OperationCanceledException) { /* normal */ } catch (Exception ex) {
                SessionLogBuffer.Append($"Feed Fehler: {ex.Message}");
                await Task.Delay(2000, ct);
            }
        }

        SessionLogBuffer.Append("Feed: Snapshot-Poll gestoppt.");
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------
    private static decimal? TryGetDecimal(JsonElement obj, string key) {
        if (!obj.TryGetProperty(key, out var v)) return null;

        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
            return d;

        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var ds))
            return ds;

        return null;
    }

    private async Task PersistBarAsync(int instrumentId, DateTime tsMinuteUtc, decimal O, decimal H, decimal L, decimal C, long V) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ICT-Felder: New York Zeit + Session/Killzone (ohne NuGet)
        var tsNy = IctTime.ToNy(tsMinuteUtc);
        var session = IctTime.SessionOf(tsNy);
        var kz = IctTime.KillzoneOf(tsNy);

        // UPSERT
        var bar = await db.Bars1m.FindAsync(new object?[] { instrumentId, tsMinuteUtc });
        if (bar is null) {
            bar = new Bar1m {
                InstrumentId = instrumentId,
                TsUtc = tsMinuteUtc,
                TsNy = tsNy,
                Open = O,
                High = H,
                Low = L,
                Close = C,
                Volume = V,
                Session = session,
                Killzone = kz,
                IngestSource = "realtime"
            };
            db.Bars1m.Add(bar);
        } else {
            bar.TsNy = tsNy;
            bar.Open = O; bar.High = H; bar.Low = L; bar.Close = C;
            bar.Volume = V;
            bar.Session = session;
            bar.Killzone = kz;
        }

        await db.SaveChangesAsync();

        // FeedState.LastRealtimeTsUtc aktualisieren
        var fs = await db.FeedStates.FindAsync(instrumentId) ?? new FeedState { InstrumentId = instrumentId };
        fs.LastRealtimeTsUtc = tsMinuteUtc;
        db.Update(fs);
        await db.SaveChangesAsync();
    }

    private async Task<string> GetDesiredAsync(CancellationToken ct) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.AppSettings.FindAsync(new object?[] { "MarketDataDesiredState" }, ct);
        return s?.Value ?? "Stopped";
    }

    private async Task SetFeedStateAsync(FeedRunState state, string? error) {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
}
