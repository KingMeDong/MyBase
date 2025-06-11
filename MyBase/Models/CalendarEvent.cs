using System;
using System.ComponentModel.DataAnnotations;

namespace MyBase.Models {
    public class CalendarEvent {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Location { get; set; }
        public bool IsReminderEnabled { get; set; } = false;

        public int? ReminderMinutesBefore { get; set; }
        public string? ReminderEmailAddress { get; set; }
        public bool ReminderSent { get; set; } = false;


    }
}
