using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Models;
using MyBase.Data;

namespace MyBase.Pages.SmartHome.Devices {
    public class CreateModel : PageModel {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context) {
            _context = context;
        }

        [BindProperty]
        public SmartDevice Device { get; set; } = new();

        public IActionResult OnGet() {
            return Page();
        }

        public IActionResult OnPost() {
            if (!ModelState.IsValid) return Page();

            _context.SmartDevices.Add(Device);
            _context.SaveChanges();

            return RedirectToPage("Index");
        }
    }
}
