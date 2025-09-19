// irgendwo im Projekt, z. B. Services/MarketData/SessionLogBuffer.cs
using System.Text;

public static class SessionLogBuffer {
    private static readonly object _lock = new();
    private static readonly StringBuilder _sb = new();
    private const int MaxLen = 32_000; // ~32 KB

    public static void Append(string line) {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        lock (_lock) {
            _sb.Append('[').Append(stamp).Append("] ").AppendLine(line);
            if (_sb.Length > MaxLen) {
                // vorne einkürzen
                var cut = _sb.Length - MaxLen;
                _sb.Remove(0, cut);
            }
        }
    }

    public static string ReadAll() {
        lock (_lock) return _sb.ToString();
    }
}
