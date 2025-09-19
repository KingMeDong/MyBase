namespace MyBase.Services.MarketData;

/// <summary>
/// Laufzustand der CPAPI-Session (State Machine).
/// </summary>
public enum SessionState {
    Disconnected, // (keine Verbindung/Gateway nicht erreichbar)
    NeedsLogin,   // (Gateway läuft, aber nicht authentifiziert → einmal im Browser MFA)
    Connecting,   // (authentifiziert, Brokerage-Session wird aufgebaut)
    Connected,    // (voll verbunden – Marktdaten erlaubt)
    Error         // (Fehlerzustand; wird geloggt + mit Backoff neu probiert)
}

/// <summary>
/// Laufzustand des Marktdaten-Feeds (Start/Stop-Logik).
/// </summary>
public enum FeedRunState : byte {
    Stopped = 0,  // (Feed ist angehalten)
    Running = 1,  // (Feed läuft)
    Error = 2   // (Fehler; Details in FeedState.LastError)
}

/// <summary>
/// Kompakter Status für die UI/Minimal-API /api/marketdata/status.
/// </summary>
public record MarketDataStatusDto(
    string Desired,            // ("Running" | "Stopped" – gewünschter Zustand aus AppSetting)
    string Session,            // (SessionState als Text)
    string Feed,               // ("Running" | "Stopped" | "Error")
    DateTime? LastHeartbeatUtc,
    DateTime? LastRealtimeBarUtc,
    string? LastError
);
