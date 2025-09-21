namespace MyBase.Services.MarketData;

public class MinuteBarBuilder {
    private readonly object _lock = new();
    private DateTime _currentMinuteUtc = DateTime.MinValue;
    private decimal _open, _high, _low, _close;
    private long _volDelta;
    private long _lastTotalVolume = -1;

    public (DateTime minuteUtc, decimal O, decimal H, decimal L, decimal C, long V)?
        PushTick(DateTime tsUtc, decimal lastPrice, long totalVolume) {
        lock (_lock) {
            var minute = new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, tsUtc.Hour, tsUtc.Minute, 0, DateTimeKind.Utc);

            if (_currentMinuteUtc == DateTime.MinValue) {
                _currentMinuteUtc = minute;
                _open = _high = _low = _close = lastPrice;
                _volDelta = 0;
                _lastTotalVolume = totalVolume;
                return null;
            }

            if (minute == _currentMinuteUtc) {
                _close = lastPrice;
                if (lastPrice > _high) _high = lastPrice;
                if (lastPrice < _low) _low = lastPrice;

                if (_lastTotalVolume >= 0 && totalVolume >= _lastTotalVolume)
                    _volDelta += (totalVolume - _lastTotalVolume);
                _lastTotalVolume = totalVolume;
                return null;
            }

            var finished = (_currentMinuteUtc, _open, _high, _low, _close, _volDelta);

            _currentMinuteUtc = minute;
            _open = _high = _low = _close = lastPrice;
            _volDelta = 0;
            if (_lastTotalVolume >= 0 && totalVolume >= _lastTotalVolume)
                _volDelta += (totalVolume - _lastTotalVolume);
            _lastTotalVolume = totalVolume;

            return finished;
        }
    }
}
