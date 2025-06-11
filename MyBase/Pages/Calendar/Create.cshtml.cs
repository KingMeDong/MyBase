using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Data;
using MyBase.Models;
using System.Threading.Tasks;

namespace MyBase.Pages.Calendar {
    public class CreateModel : PageModel {
        private readonly AppDbContext _dbContext;

        public CreateModel(AppDbContext dbContext) {
            _dbContext = dbContext;
        }

        [BindProperty]
        public CalendarEvent Event { get; set; } = new();

        public void OnGet() {
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            int userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

            Event.UserId = userId;

            _dbContext.CalendarEvents.Add(Event);
            await _dbContext.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
