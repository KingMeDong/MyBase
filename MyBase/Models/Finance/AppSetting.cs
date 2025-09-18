using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyBase.Models.Finance;

[Table("AppSetting")]
public class AppSetting {
    [Key] public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
