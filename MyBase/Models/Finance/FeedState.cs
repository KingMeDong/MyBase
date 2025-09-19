using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Metrics;

namespace MyBase.Models.Finance;

[Table("FeedState")]
public class FeedState {
    [Key, ForeignKey(nameof(Instrument))]
    public int InstrumentId { get; set; }

    public byte Status { get; set; } = 0; // 0=Stopped, 1=Running, 2=Error
    public DateTime? LastRealtimeTsUtc { get; set; }
    public DateTime? LastBackfillToUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public string? LastError { get; set; }

    public Instrument? Instrument { get; set; }
}
