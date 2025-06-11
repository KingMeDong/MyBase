using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Data;
using MyBase.Models;
using System.Threading.Tasks;

namespace MyBase.Pages.Calendar {
    public class DetailsModel : PageModel {
        private readonly AppDbContext _dbContext;

        public DetailsModel(AppDbContext dbContext) {
            _dbContext = dbContext;
        }

        [BindProperty]
        public CalendarEvent Event { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id) {
            int userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

            Event = await _dbContext.CalendarEvents.FindAsync(id);

            if (Event == null || Event.UserId != userId) {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            int userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

            var calendarEvent = await _dbContext.CalendarEvents.FindAsync(Event.Id);

            if (calendarEvent == null || calendarEvent.UserId != userId) {
                return NotFound();
            }

            // Werte aktualisieren
            calendarEvent.Title = Event.Title;
            calendarEvent.Description = Event.Description;
            calendarEvent.Location = Event.Location;
            calendarEvent.StartDateTime = Event.StartDateTime;
            calendarEvent.EndDateTime = Event.EndDateTime;


            calendarEvent.IsReminderEnabled = Event.IsReminderEnabled;
            calendarEvent.ReminderMinutesBefore = Event.ReminderMinutesBefore;
            calendarEvent.ReminderEmailAddress = Event.ReminderEmailAddress;


            await _dbContext.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
