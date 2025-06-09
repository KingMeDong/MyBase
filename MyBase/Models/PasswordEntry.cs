using System;
using System.ComponentModel.DataAnnotations;

namespace MyBase.Models {
    public class PasswordEntry {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Url { get; set; }

        public string Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public int UserId { get; set; }

    }
}
