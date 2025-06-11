using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBase.Pages.Calendar {
    public class IndexModel : PageModel {
        private readonly AppDbContext _dbContext;

        public IndexModel(AppDbContext dbContext) {
            _dbContext = dbContext;
        }

        public List<CalendarEvent> Events { get; set; } = new();

        public async Task OnGetAsync() {
            int userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

            Events = await _dbContext.CalendarEvents
                .Where(e => e.UserId == userId)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();
        }



        public async Task<IActionResult> OnPostDeleteAsync(int id) {
            var calendarEvent = await _dbContext.CalendarEvents.FindAsync(id);

            if (calendarEvent != null) {
                int userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

                // Nur eigenen Termin löschen
                if (calendarEvent.UserId == userId) {
                    _dbContext.CalendarEvents.Remove(calendarEvent);
                    await _dbContext.SaveChangesAsync();
                }
            }

            return RedirectToPage();
        }

    }
}
