using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyBase.Models {
    public class CodeRepository {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<CodeSnapshot> Snapshots { get; set; } = new();
    }

    public class CodeSnapshot {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;  // z.B. snapshot_2025-07-14.zip

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CodeRepositoryId { get; set; }
        public CodeRepository? CodeRepository { get; set; }
    }
}
