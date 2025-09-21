using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyBase.Models.Finance;

[Table("Bar_1m")]
public class Bar1m {
    [Key, Column(Order = 0)]
    public int InstrumentId { get; set; }

    [Key, Column(Order = 1)]
    public DateTime TsUtc { get; set; }   // Minuten-Zeitstempel (UTC, 00..59 sek)

    public DateTime TsNy { get; set; }    // New York Zeit (für ICT-Auswertung)

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }

    public long Volume { get; set; }      // Minuten-Volumen (Delta, nicht total)
    public byte Session { get; set; }     // 0=ETH, 1=RTH
    public byte Killzone { get; set; }    // 0=none, 1=London AM, 2=NY AM, 3=NY PM

    [MaxLength(20)]
    public string IngestSource { get; set; } = "realtime";

    [ForeignKey(nameof(InstrumentId))]
    public Instrument? Instrument { get; set; }
}
