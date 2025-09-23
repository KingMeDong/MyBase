using System;

namespace MyBase.Services.MarketData;

/// <summary>
/// Zeit-Utilities für ICT-Logik (NY-Zeit, Session, Killzones) – ohne NuGet.
/// Versucht erst Windows-ID ("Eastern Standard Time"), dann IANA ("America/New_York").
/// </summary>
public static class IctTime {
    private static readonly TimeZoneInfo NyTz = ResolveNyTimeZone();
    public static byte GetSession(DateTime ny) => SessionOf(ny);
    public static byte GetKillzone(DateTime ny) => KillzoneOf(ny);

    private static TimeZoneInfo ResolveNyTimeZone() {
        // (1) Windows-Name
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { /* ignore */ }

        // (2) IANA-Name (Linux/Mac)
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { /* ignore */ }

        // (3) Fallback: UTC (funktioniert, aber Session/KZ wären dann nicht NY-basiert)
        return TimeZoneInfo.Utc;
    }

    /// <summary>(UTC → New York-Zeit)</summary>
    public static DateTime ToNy(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NyTz);

    /// <summary>Session: 1 = RTH (09:30–16:00 NY), 0 = ETH</summary>
    public static byte SessionOf(DateTime ny)
        => (ny.TimeOfDay >= new TimeSpan(9, 30, 0) && ny.TimeOfDay < new TimeSpan(16, 0, 0)) ? (byte)1 : (byte)0;

    /// <summary>Killzones (grobe Defaults, kannst du feinjustieren)</summary>
    public static byte KillzoneOf(DateTime ny) {
        var t = ny.TimeOfDay;
        if (t >= new TimeSpan(2, 0, 0) && t < new TimeSpan(5, 0, 0)) return 1; // London AM
        if (t >= new TimeSpan(7, 0, 0) && t < new TimeSpan(10, 0, 0)) return 2; // NY AM
        if (t >= new TimeSpan(13, 0, 0) && t < new TimeSpan(15, 0, 0)) return 3; // NY PM
        return 0;
    }
}
