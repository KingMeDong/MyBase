using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBase.Data;
using MyBase.Models.Finance;

namespace MyBase.Services.MarketData {
    public sealed class BackfillWorker : BackgroundService {
        private readonly IServiceProvider _sp;
        private readonly ILogger<BackfillWorker> _log;
        private readonly Channel<BackfillRequest> _queue;
        private readonly BackfillStatusStore _status;
        private readonly IHttpClientFactory _http;

        public BackfillWorker(
            IServiceProvider sp,
            ILogger<BackfillWorker> log,
            Channel<BackfillRequest> queue,
            BackfillStatusStore statusStore,
            IHttpClientFactory http) {
            _sp = sp;
            _log = log;
            _queue = queue;
            _status = statusStore;
            _http = http;
        }

        protected override async Task ExecuteAsync(CancellationToken ct) {
            SessionLogBuffer.Append("BackfillWorker: gestartet (warte auf Aufträge).");

            while (!ct.IsCancellationRequested) {
                BackfillRequest req;
                try { req = await _queue.Reader.ReadAsync(ct); } catch (OperationCanceledException) { break; }

                var st = _status.Create(req);
                st.SetState(BackfillJobState.Running);

                try {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var inst = await db.Instruments.AsNoTracking()
                        .FirstOrDefaultAsync(i => i.Id == req.InstrumentId, ct);
                    if (inst is null) {
                        Fail(st, "Instrument nicht gefunden.");
                        continue;
                    }

                    st.InstrumentSymbol = inst.Symbol;
                    if (req.EndUtc <= req.StartUtc) {
                        Fail(st, "Zeitraum ungültig (End<=Start).");
                        continue;
                    }

                    var segments = EnumerateDailySegments(req.StartUtc, req.EndUtc).ToArray();
                    st.SegmentsPlanned = segments.Length;

                    SessionLogBuffer.Append(
                        $"BackfillWorker: Job angenommen (JobId={st.JobId}, {inst.Symbol}, {req.StartUtc:u} → {req.EndUtc:u}, RTH={req.RthOnly}); Plane {segments.Length} Segmente.");

                    var client = _http.CreateClient("Cpapi");

                    foreach (var seg in segments) {
                        ct.ThrowIfCancellationRequested();
                        SessionLogBuffer.Append($"BackfillWorker: Segment {seg.from:u} → {seg.to:u} (RTH={req.RthOnly}) …");

                        // Try HMDS (stabiler Pfad)
                        var candles = await FetchHmdsBarsAsync(client, inst.IbConId, seg.from, req.RthOnly, ct);
                        if (candles.Count == 0) {
                            SessionLogBuffer.Append("BackfillWorker: Hinweis – keine Bars im Segment (evtl. Wochenende/Feiertag/API miss).");
                            st.IncSegmentDone();
                            continue;
                        }

                        // existierende Ts sammeln (nur Segment-Range)
                        var minTs = seg.from;
                        var maxTs = seg.to;
                        var existingTs = await db.Bars1m
                            .Where(b => b.InstrumentId == inst.Id && b.TsUtc >= minTs && b.TsUtc <= maxTs)
                            .Select(b => b.TsUtc)
                            .ToListAsync(ct);
                        var existing = new HashSet<DateTime>(existingTs);

                        var toInsert = new List<Bar1m>(candles.Count);
                        var toUpdate = new List<Bar1m>();

                        foreach (var c in candles) {
                            if (c.TsUtc < seg.from || c.TsUtc > seg.to) continue;

                            var tsNy = IctTime.ToNy(c.TsUtc);
                            var sess = IctTime.SessionOf(tsNy);
                            var kz = IctTime.KillzoneOf(tsNy);

                            var row = new Bar1m {
                                InstrumentId = inst.Id,
                                TsUtc = c.TsUtc,
                                TsNy = tsNy,
                                Open = (decimal)c.O,
                                High = (decimal)c.H,
                                Low = (decimal)c.L,
                                Close = (decimal)c.C,
                                Volume = c.V,
                                Session = sess,
                                Killzone = kz,
                                IngestSource = req.Source ?? "backfill"
                            };

                            if (existing.Contains(c.TsUtc)) toUpdate.Add(row); else toInsert.Add(row);
                        }

                        if (toInsert.Count > 0) {
                            await db.Bars1m.AddRangeAsync(toInsert, ct);
                            st.AddInserted(toInsert.Count);
                        }

                        if (toUpdate.Count > 0) {
                            foreach (var e in toUpdate) {
                                db.Bars1m.Attach(e);
                                db.Entry(e).Property(x => x.TsNy).IsModified = true;
                                db.Entry(e).Property(x => x.Open).IsModified = true;
                                db.Entry(e).Property(x => x.High).IsModified = true;
                                db.Entry(e).Property(x => x.Low).IsModified = true;
                                db.Entry(e).Property(x => x.Close).IsModified = true;
                                db.Entry(e).Property(x => x.Volume).IsModified = true;
                                db.Entry(e).Property(x => x.Session).IsModified = true;
                                db.Entry(e).Property(x => x.Killzone).IsModified = true;
                                db.Entry(e).Property(x => x.IngestSource).IsModified = true;
                            }
                            st.AddUpserts(toUpdate.Count);
                        }

                        await db.SaveChangesAsync(ct);
                        SessionLogBuffer.Append($"BackfillWorker: Segment gespeichert (inserted={toInsert.Count}, updated={toUpdate.Count}).");
                        st.IncSegmentDone();
                    }

                    _status.TrySetFinished(st.JobId, BackfillJobState.Done);
                    SessionLogBuffer.Append($"BackfillWorker: Job abgeschlossen (JobId={st.JobId}).");
                } catch (OperationCanceledException) { /* shutdown */ } catch (Exception ex) {
                    _log.LogError(ex, "BackfillWorker: unerwarteter Fehler.");
                    SessionLogBuffer.Append($"BackfillWorker: UNERWARTETER FEHLER – {ex.Message}");
                    _status.TrySetFinished(st.JobId, BackfillJobState.Failed, ex.Message);
                }
            }

            SessionLogBuffer.Append("BackfillWorker: gestoppt.");

            static void Fail(BackfillStatus st, string msg) {
                st.Error = msg; st.SetState(BackfillJobState.Failed); st.FinishedUtc = DateTime.UtcNow;
                SessionLogBuffer.Append($"BackfillWorker: JOB FEHLER – {msg}");
            }
        }

        // ---------------- HMDS (einziger Pfad) ----------------

        private static async Task<List<Candle>> FetchHmdsBarsAsync(
            HttpClient client, long conId, DateTime segStartUtc, bool rthOnly, CancellationToken ct) {
            // endTime bestimmen
            var endUtc = ComputeEndUtcForSegment(segStartUtc, rthOnly);
            SessionLogBuffer.Append(
    $"BackfillWorker: History-Call für {segStartUtc:yyyy-MM-dd} (RTH={rthOnly}) mit endTime={endUtc:yyyy-MM-dd HH:mm:ss}Z");

            var endStr = Uri.EscapeDataString(endUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var url = $"/v1/api/hmds/history?conid={conId}&bar=1min&period=1d&outsideRth={(!rthOnly).ToString().ToLowerInvariant()}&endTime={endStr}";

            // kleine Retry-Logik gegen 5xx
            for (int attempt = 1; attempt <= 3; attempt++) {
                try {
                    using var r = await client.GetAsync(url, ct);
                    string body = await r.Content.ReadAsStringAsync(ct);

                    if (r.IsSuccessStatusCode) {
                        var list = ParseHistory(body);
                        SessionLogBuffer.Append($"BackfillWorker: History OK ({list.Count} Bars) – {url}");
                        return list;
                    }

                    if ((int)r.StatusCode >= 500) {
                        SessionLogBuffer.Append($"BackfillWorker: hmds/history {(int)r.StatusCode} – Versuch {attempt}/3; warte …");
                        await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                        continue;
                    }

                    SessionLogBuffer.Append($"BackfillWorker: hmds/history miss {(int)r.StatusCode} – {url} :: {TrimForLog(body)}");
                    return new List<Candle>();
                } catch (Exception ex) when (attempt < 3) {
                    SessionLogBuffer.Append($"BackfillWorker: hmds/history EX (Versuch {attempt}/3) – {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), ct);
                } catch (Exception ex) {
                    SessionLogBuffer.Append($"BackfillWorker: hmds/history EX – {ex.Message}");
                    break;
                }
            }

            return new List<Candle>();
        }

        private static DateTime ComputeEndUtcForSegment(DateTime segStartUtc, bool rthOnly) {
            var tz = ResolveNyTz();

            // Wichtig: erst den Segmentstart in NY-Zeit umrechnen und dann das NY-Datum nehmen.
            var nyStart = TimeZoneInfo.ConvertTimeFromUtc(segStartUtc, tz);
            var nyDate = nyStart.Date;

            // RTH = Handel 09:30–16:00 NY -> endTime = 16:00 NY des Segmenttages
            // ETH = "alles" -> endTime = 23:59 NY des Segmenttages (sicher im gleichen Kalendertag)
            var nyEndLocal = rthOnly
                ? new DateTime(nyDate.Year, nyDate.Month, nyDate.Day, 16, 0, 0, DateTimeKind.Unspecified)
                : new DateTime(nyDate.Year, nyDate.Month, nyDate.Day, 23, 59, 0, DateTimeKind.Unspecified);
            
            return TimeZoneInfo.ConvertTimeToUtc(nyEndLocal, tz);
        }


        private static TimeZoneInfo ResolveNyTz() {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { }
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { }
            return TimeZoneInfo.Utc;
        }

        // ---------------- Parsing & Helfer ----------------

        private static readonly JsonSerializerOptions JsonOpts =
            new(JsonSerializerDefaults.Web) {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

        private static List<Candle> ParseHistory(string json) {
            try {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // einige Gateways liefern { "data": [...] }
                var arr = root;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d))
                    arr = d;

                if (arr.ValueKind != JsonValueKind.Array) return new List<Candle>();

                var list = new List<Candle>(arr.GetArrayLength());
                foreach (var el in arr.EnumerateArray())
                    if (TryParseCandle(el, out var c)) list.Add(c);

                return list;
            } catch {
                return new List<Candle>();
            }
        }

        private static bool TryParseCandle(JsonElement el, out Candle c) {
            c = default;

            if (!TryParseTs(el, "t", out var tsUtc)) return false;
            if (!TryGetDouble(el, "o", out var o)) return false;
            if (!TryGetDouble(el, "h", out var h)) return false;
            if (!TryGetDouble(el, "l", out var l)) return false;
            if (!TryGetDouble(el, "c", out var close)) return false;
            if (!TryGetLong(el, "v", out var vol)) vol = 0;

            c = new Candle {
                TsUtc = tsUtc,
                O = o,
                H = h,
                L = l,
                C = close,
                V = (int)Math.Max(0, Math.Min(int.MaxValue, vol))
            };
            return true;
        }

        private static bool TryParseTs(JsonElement el, string name, out DateTime tsUtc) {
            tsUtc = default;
            if (!el.TryGetProperty(name, out var p)) return false;

            if (p.ValueKind == JsonValueKind.String) {
                var s = p.GetString()!;
                // häufige HMDS-Formate
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out tsUtc))
                    return true;

                if (DateTime.TryParseExact(s, "yyyyMMdd-HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var a)) {
                    tsUtc = DateTime.SpecifyKind(a, DateTimeKind.Utc);
                    return true;
                }
                if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var b)) {
                    tsUtc = DateTime.SpecifyKind(b, DateTimeKind.Utc);
                    return true;
                }
            } else if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var ms)) {
                tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                return true;
            }

            return false;
        }

        private static bool TryGetDouble(JsonElement el, string name, out double v) {
            v = 0;
            if (!el.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out v)) return true;
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return true;
            return false;
        }

        private static bool TryGetLong(JsonElement el, string name, out long v) {
            v = 0;
            if (!el.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out v)) return true;
            if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return true;
            return false;
        }

        private static IEnumerable<(DateTime from, DateTime to)> EnumerateDailySegments(DateTime startUtc, DateTime endUtc) {
            var d0 = startUtc.Date;
            var d1 = endUtc.Date;
            for (var d = d0; d <= d1; d = d.AddDays(1))
                yield return (d, d.AddDays(1).AddMinutes(-1)); // 23:59 UTC des Tages
        }

        private static string TrimForLog(string s)
            => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= 140 ? s : s[..140] + " …");

        private readonly struct Candle {
            public DateTime TsUtc { get; init; }
            public double O { get; init; }
            public double H { get; init; }
            public double L { get; init; }
            public double C { get; init; }
            public int V { get; init; }
        }
    }
}
