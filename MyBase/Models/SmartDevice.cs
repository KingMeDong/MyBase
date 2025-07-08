using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;


namespace MyBase.Models {
    public class SmartDevice {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty; // "Zigbee" oder "Pico"

        [Required]
        public string ControlType { get; set; } = string.Empty; // "switch", "slider", "display"

        [Required]
        public string Endpoint { get; set; } = string.Empty; // URL oder ioBroker-State-ID

        public string? Description { get; set; }

        public int? Min { get; set; } // Für Regler
        public int? Max { get; set; }

        public string? Code { get; set; }


        public int? RoomId { get; set; } // optional, damit kein Raum auch möglich ist
        public Room? Room { get; set; }

    }
}
