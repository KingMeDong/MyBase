using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyBase.Models.Finance;

[Table("Instrument")]
public class Instrument {
    [Key] public int Id { get; set; }

    [MaxLength(20)] public string Symbol { get; set; } = default!;
    [MaxLength(10)] public string SecType { get; set; } = default!;   // STK/FUT
    [MaxLength(50)] public string Exchange { get; set; } = default!;
    [MaxLength(10)] public string Currency { get; set; } = default!;
    public long IbConId { get; set; }

    public decimal? TickSize { get; set; }
    public decimal? Multiplier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
