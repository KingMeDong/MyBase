using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Linq;

// Log-Level (für menschlich lesbare Einordnung)
public enum LogLevel { Debug, Info, Warn, Error }

public static class SessionLogBuffer {
    private static readonly object _lock = new();
    private static readonly StringBuilder _sb = new();
    private const int MaxLen = 32_000;

    private static readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private static CancellationTokenSource? _cts;
    private static Task? _writerTask;
    private static string _logDir = "";
    private static string _currentFile = "";
    private static DateTime _curDate = DateTime.MinValue;
    private static bool _configured;

    // ---- UI-Suppress (Ping-Entlastung) ----
    // Diese Einträge werden im UI-Buffer unterdrückt, aber weiter in die Datei geschrieben.
    private static readonly string[] _uiSuppressEquals = {
        "SSO validate: OK",
    };
    private static readonly string[] _uiSuppressPrefixes = {
        "Tickle: 200",   // z. B. "Tickle: 200 OK"
    };

    // Zähler für zusammengefasste Meldungen (alle 30s Kurz-Summary ins UI)
    private static readonly object _sLock = new();
    private static readonly Dictionary<string, (int Count, DateTime NextEmit, string Label)> _sCounters = new();
    private static readonly TimeSpan _summaryInterval = TimeSpan.FromSeconds(30);

    // ---- Throttle-Schlüssel (für SessionLogBuffer.Throttled) ----
    private static readonly Dictionary<string, DateTime> _next = new();

    // --- Initialisierung über Program.cs ---
    public static void Configure(IConfiguration cfg) {
        try {
            var basePath = cfg["SystemSettings:FileManagerPath"];
            if (string.IsNullOrWhiteSpace(basePath)) {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                basePath = Path.Combine(local, "MyBase", "AllFiles");
            }

            _logDir = Path.Combine(basePath!, "Logs");
            Directory.CreateDirectory(_logDir);

            _curDate = DateTime.UtcNow.Date;
            _currentFile = Path.Combine(_logDir, $"ict-trader_{_curDate:yyyy-MM-dd}.log");

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => WriterLoop(_cts.Token));

            _configured = true;
            Append("SessionLogBuffer initialized.");
        } catch {
            // keine Exceptions nach außen
        }
    }

    // =====================================================================
    // =============  LOGGING v1 – strukturierte Helfer  ====================
    // =====================================================================

    // Komfort-APIs (verwenden intern AppendCore -> Append)
    public static void Debug(string comp, string evt, string msg, params (string k, object? v)[] kv)
        => AppendCore(LogLevel.Debug, comp, evt, msg, kv);

    public static void Info(string comp, string evt, string msg, params (string k, object? v)[] kv)
        => AppendCore(LogLevel.Info, comp, evt, msg, kv);

    public static void Warn(string comp, string evt, string msg, params (string k, object? v)[] kv)
        => AppendCore(LogLevel.Warn, comp, evt, msg, kv);

    public static void Error(string comp, string evt, string msg, params (string k, object? v)[] kv)
        => AppendCore(LogLevel.Error, comp, evt, msg, kv);

    // Gedrosseltes Logging (z. B. nur 1x pro 5s) – Key steuert die Drosselgruppe
    public static void Throttled(string key, TimeSpan minInterval,
        LogLevel lvl, string comp, string evt, string msg, params (string k, object? v)[] kv) {
        var now = DateTime.UtcNow;
        lock (_next) {
            if (_next.TryGetValue(key, out var until) && now < until) return;
            _next[key] = now + minInterval;
        }
        AppendCore(lvl, comp, evt, msg, kv);
    }

    // Baut die v1-Zeile und leitet an Append weiter
    private static void AppendCore(LogLevel lvl, string comp, string evt, string msg, params (string k, object? v)[] kv) {
        var line = $"[{lvl}][{comp}][{evt}] {msg}";
        if (kv is { Length: > 0 }) {
            // (Schlüssel/Werte strukturiert; Werte werden „sanitized“ (keine CR/LF))
            line += " | " + string.Join(' ', kv.Select(p => $"{p.k}={Sanitize(p.v)}"));
        }
        Append(line);
    }

    private static string Sanitize(object? v)
        => v is null ? "null" : v.ToString()!.Replace("\r", " ").Replace("\n", " ").Trim();

    // =====================================================================
    // ==============  Kern-API – bleibt rückwärtskompatibel  ==============
    // =====================================================================

    // Schreibt in Datei (immer) + UI-Buffer (mit Suppression/Summary)
    public static void Append(string line) {
        var nowLocal = DateTime.Now;
        var nowUtc = DateTime.UtcNow;

        // 1) Datei-Log: IMMER (vollständige Nachvollziehbarkeit)
        var fileLine = $"[{nowLocal:yyyy-MM-dd HH:mm:ss.fff} {TimeZoneInfo.Local.Id}] [{nowUtc:HH:mm:ss.fff}Z] {line}";
        try { _queue.Add(fileLine); } catch { }

        // 2) UI-Log: ggf. unterdrücken (Ping-Entlastung) + Summaries
        bool suppressUi = ShouldSuppressForUi(line, out var supKey, out var supLabel);
        if (suppressUi) {
            BumpSuppressionCounter(supKey!, supLabel!);
            TryEmitSummaries();
            return; // diese Einzelzeile NICHT ins UI schreiben
        }

        // Normale UI-Zeile
        var uiLine = $"[{nowLocal:HH:mm:ss}] {line}";
        AppendUiOnly(uiLine);

        // Kurz-Summaries (falls fällig) hinzufügen
        TryEmitSummaries();
    }

    // --- UI-Buffer lesen ---
    public static string ReadAll() {
        lock (_lock) return _sb.ToString();
    }

    public static void Stop() {
        try {
            _cts?.Cancel();
            _writerTask?.Wait(1000);
        } catch { }
    }

    // ---------------- private helpers ----------------

    private static void AppendUiOnly(string uiLine) {
        lock (_lock) {
            _sb.AppendLine(uiLine);
            if (_sb.Length > MaxLen) {
                var cut = _sb.Length - MaxLen;
                _sb.Remove(0, cut);
            }
        }
    }

    private static bool ShouldSuppressForUi(string line, out string? key, out string? label) {
        var trimmed = line.Trim();

        foreach (var eq in _uiSuppressEquals) {
            if (trimmed.Equals(eq, StringComparison.Ordinal)) {
                key = "eq:" + eq;
                label = eq;
                return true;
            }
        }
        foreach (var pfx in _uiSuppressPrefixes) {
            if (trimmed.StartsWith(pfx, StringComparison.Ordinal)) {
                key = "pfx:" + pfx;
                label = pfx + " …";
                return true;
            }
        }

        key = null; label = null;
        return false;
    }

    private static void BumpSuppressionCounter(string key, string label) {
        lock (_sLock) {
            if (_sCounters.TryGetValue(key, out var c))
                _sCounters[key] = (c.Count + 1, c.NextEmit, label);
            else
                _sCounters[key] = (1, DateTime.UtcNow + _summaryInterval, label);
        }
    }

    // gibt alle fälligen Kurz-Summaries in den UI-Buffer (nicht in Datei)
    private static void TryEmitSummaries() {
        List<(string Label, int Count)> due = new();
        lock (_sLock) {
            var now = DateTime.UtcNow;
            foreach (var kv in _sCounters.ToArray()) {
                var (count, next, label) = kv.Value;
                if (count > 0 && now >= next) {
                    due.Add((label, count));
                    _sCounters[kv.Key] = (0, now + _summaryInterval, label);
                }
            }
        }
        if (due.Count > 0) {
            var nowLocal = DateTime.Now;
            foreach (var d in due) {
                AppendUiOnly($"[{nowLocal:HH:mm:ss}] [throttled] {d.Label} x{d.Count}");
            }
        }
    }

    // --- Hintergrund-Writer (Datei) ---
    private static void WriterLoop(CancellationToken ct) {
        StreamWriter? sw = null;

        try {
            foreach (var line in _queue.GetConsumingEnumerable(ct)) {
                var today = DateTime.UtcNow.Date;
                if (today != _curDate) {
                    sw?.Dispose();
                    _curDate = today;
                    _currentFile = Path.Combine(_logDir, $"ict-trader_{today:yyyy-MM-dd}.log");
                    sw = null;
                }

                sw ??= new StreamWriter(
                    new FileStream(_currentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };

                sw.WriteLine(line);
            }
        } catch (OperationCanceledException) { } catch { } finally { sw?.Dispose(); }
    }
}
