namespace MyBase.Services.MarketData;

public class CpapiOptions {
    public string BaseUrl { get; set; } = default!; // z. B. https://192.168.78.55:5000
    public int HeartbeatSeconds { get; set; } = 60; // (Keep-Alive-Takt /tickle)
    public int StatusPollSeconds { get; set; } = 180; // (seltener Status-Check)
}
