using System;

namespace MyBase.Services.MarketData;

/// <summary>
/// Auftrag für den Historien-Lauf (Backfill = nachträgliches Auffüllen).
/// </summary>
public sealed class BackfillRequest {
    public Guid JobId { get; init; } = Guid.NewGuid();
    public int InstrumentId { get; init; }
    public DateTime StartUtc { get; init; }    // inkl.
    public DateTime EndUtc { get; init; }      // inkl./exkl. ist später egal – wir runden segmentweise
    public bool RthOnly { get; init; } = true; // nur reguläre Handelszeit (09:30–16:00 NY)
    public string Source { get; init; } = "backfill";
}
