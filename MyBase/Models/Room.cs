namespace MyBase.Models {
    public class Room {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // optional: Navigationsproperty für später
        public List<SmartDevice>? Devices { get; set; }
    }
}
