namespace MyBase.Services.MarketData;

public class MinuteBarBuilder {
    private readonly object _lock = new();

    private DateTime _currentMinuteUtc = DateTime.MinValue;
    private decimal _open, _high, _low, _close;
    private long _volDelta;
    private long? _lastTotalVolume; // Tagesvolumen kumuliert (kann fehlen/Zurücksetzen)

    // ---- Spread-Sampling für die laufende Minute ----
    private decimal _spreadSum;
    private int _spreadCount;
    private decimal _spreadMax;

    /// <summary>
    /// Aggregiert Ticks zu 1m-Bars. Gibt bei Minutenwechsel die fertige Bar zurück, sonst null.
    /// - tsUtc muss UTC sein.
    /// - totalVolume ist das TAGES-Volumen (kumuliert). Wir bilden daraus das Minuten-Delta.
    /// - bid/ask optional; wenn vorhanden -> SpreadAvg/SpreadMax.
    /// </summary>
    public (DateTime minuteUtc, decimal O, decimal H, decimal L, decimal C, long V, decimal? SpreadAvg, decimal? SpreadMax)?
        PushTick(DateTime tsUtc, decimal lastPrice, long? totalVolume, decimal? bid = null, decimal? ask = null) {
        lock (_lock) {
            var minute = new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, tsUtc.Hour, tsUtc.Minute, 0, DateTimeKind.Utc);

            // Vorige Minute abschließen?
            (DateTime minuteUtc, decimal O, decimal H, decimal L, decimal C, long V, decimal? SpreadAvg, decimal? SpreadMax)? finished = null;
            if (_currentMinuteUtc != DateTime.MinValue && minute != _currentMinuteUtc) {
                decimal? spreadAvg = _spreadCount > 0 ? _spreadSum / _spreadCount : (decimal?)null;
                decimal? spreadMax = _spreadCount > 0 ? _spreadMax : (decimal?)null;
                finished = (_currentMinuteUtc, _open, _high, _low, _close, _volDelta, spreadAvg, spreadMax);
            }

            // Neue Minute initialisieren?
            if (minute != _currentMinuteUtc) {
                _currentMinuteUtc = minute;
                _open = _high = _low = _close = lastPrice;
                _volDelta = 0;

                // Spread-Accumulator zurücksetzen
                _spreadSum = 0m;
                _spreadCount = 0;
                _spreadMax = 0m;

                // Beim Minutenstart evtl. erstes Volumen-Delta mitnehmen
                if (totalVolume.HasValue && _lastTotalVolume.HasValue && totalVolume.Value >= _lastTotalVolume.Value)
                    _volDelta += (totalVolume.Value - _lastTotalVolume.Value);
                if (totalVolume.HasValue) _lastTotalVolume = totalVolume.Value;
            }

            // OHLC fortschreiben
            if (lastPrice > _high) _high = lastPrice;
            if (lastPrice < _low) _low = lastPrice;
            _close = lastPrice;

            // Minuten-Volumen (Delta aus Tagesvolumen)
            if (totalVolume.HasValue) {
                if (_lastTotalVolume.HasValue) {
                    if (totalVolume.Value >= _lastTotalVolume.Value)
                        _volDelta += (totalVolume.Value - _lastTotalVolume.Value);
                    // Tagesvolumen-Reset (neuer Handelstag/Feed-Reset) -> kein negatives Delta addieren
                }
                _lastTotalVolume = totalVolume.Value;
            }

            // Spread-Sample (nur wenn plausibel)
            if (bid.HasValue && ask.HasValue && ask.Value > bid.Value && bid.Value > 0 && ask.Value > 0) {
                var s = ask.Value - bid.Value; // Spread = Ask − Bid
                _spreadSum += s;
                _spreadCount += 1;
                if (s > _spreadMax) _spreadMax = s;
            }

            return finished;
        }
    }
}
