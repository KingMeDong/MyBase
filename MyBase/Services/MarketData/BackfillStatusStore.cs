using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MyBase.Services.MarketData {
    /// <summary>Lebenszyklus eines Backfill-Jobs.</summary>
    public enum BackfillJobState {
        Queued,
        Running,
        Done,
        Failed,
        Cancelled
    }

    /// <summary>Statusobjekt je Job (threadsicher aktualisierbar).</summary>
    public sealed class BackfillStatus {
        // Identität
        public Guid JobId { get; init; }
        public int InstrumentId { get; init; }
        public string? InstrumentSymbol { get; internal set; }

        // Zeitraum
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public bool RthOnly { get; init; }

        // Meta
        public string Source { get; init; } = "backfill"; // "backfill" | "catchup"
        public DateTime CreatedUtc { get; } = DateTime.UtcNow;
        public DateTime? FinishedUtc { get; internal set; }

        // Fortschritt
        public BackfillJobState State { get; private set; } = BackfillJobState.Queued;
        public int SegmentsPlanned { get; internal set; }
        public int SegmentsDone => _segmentsDone;
        public int Inserted => _inserted;
        public int Upserts => _upserts;
        public string? Error { get; internal set; }

        // Zähler (Interlocked = threadsichere, atomare Inkremente)
        private int _segmentsDone;
        private int _inserted;
        private int _upserts;

        internal void SetState(BackfillJobState state) => State = state;
        internal void IncSegmentDone() => Interlocked.Increment(ref _segmentsDone);
        internal void AddInserted(int n) => Interlocked.Add(ref _inserted, n);
        internal void AddUpserts(int n) => Interlocked.Add(ref _upserts, n);
    }

    /// <summary>Threadsicherer Speicher aller Backfill-Jobs (für /api/backfill/status).</summary>
    public sealed class BackfillStatusStore {
        private readonly ConcurrentDictionary<Guid, BackfillStatus> _jobs = new();

        /// <summary>
        /// Erstellt den Status anhand der ursprünglichen Request-Daten.
        /// (bleibt kompatibel zu unserem Flow: Request → Worker → Status)
        /// </summary>
        public BackfillStatus Create(BackfillRequest req) {
            var st = new BackfillStatus {
                JobId = req.JobId,
                InstrumentId = req.InstrumentId,
                StartUtc = req.StartUtc,
                EndUtc = req.EndUtc,
                RthOnly = req.RthOnly,
                // Source aus Request übernehmen (default "backfill" / optional "catchup")
                // InstrumentSymbol füllen wir im Worker, sobald wir das Instrument geladen haben
                Source = req.Source ?? "backfill"
            };
            _jobs[st.JobId] = st;
            return st;
        }

        public BackfillStatus? Get(Guid id) =>
            _jobs.TryGetValue(id, out var st) ? st : null;

        public bool TrySetFinished(Guid id, BackfillJobState finalState, string? error = null) {
            if (!_jobs.TryGetValue(id, out var st)) return false;
            st.Error = error;
            st.SetState(finalState);
            st.FinishedUtc = DateTime.UtcNow;
            return true;
        }

        /// <summary>Optional: alte Jobs nach Ablauf entfernen (Housekeeping).</summary>
        public int CleanupOlderThan(TimeSpan maxAge) {
            var now = DateTime.UtcNow;
            var removed = 0;
            foreach (var kv in _jobs) {
                var st = kv.Value;
                var end = st.FinishedUtc ?? st.CreatedUtc;
                if (now - end > maxAge && _jobs.TryRemove(kv.Key, out _))
                    removed++;
            }
            return removed;
        }
    }
}
