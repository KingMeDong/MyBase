using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models;
using System.Threading.Tasks;

namespace MyBase.Pages.SmartHome.Devices {
    public class EditModel : PageModel {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context) {
            _context = context;
        }

        [BindProperty]
        public SmartDevice Device { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id) {
            Device = await _context.SmartDevices.FirstOrDefaultAsync(d => d.Id == id);
            if (Device == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) return Page();

            var existing = await _context.SmartDevices.FindAsync(Device.Id);
            if (existing == null) return NotFound();

            _context.Entry(existing).CurrentValues.SetValues(Device);
            await _context.SaveChangesAsync();

            return RedirectToPage("Index");
        }
        public async Task<IActionResult> OnPostDeleteAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null) return NotFound();

            _context.SmartDevices.Remove(device);
            await _context.SaveChangesAsync();

            return RedirectToPage("Index");
        }

    }
}
