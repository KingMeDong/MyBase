using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models;
using System.Net.Http.Headers;

namespace MyBase.Pages.SmartHome.Devices {
    public class EditModel : PageModel {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context) {
            _context = context;
        }

        [BindProperty]
        public SmartDevice Device { get; set; } = new();

        public List<Room> Rooms { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id) {
            Device = await _context.SmartDevices
                .Include(d => d.Room)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (Device == null) return NotFound();

            Rooms = await _context.Rooms.OrderBy(r => r.Name).ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                Rooms = await _context.Rooms.OrderBy(r => r.Name).ToListAsync();
                return Page();
            }

            var existing = await _context.SmartDevices.FindAsync(Device.Id);
            if (existing == null) return NotFound();

            _context.Entry(existing).CurrentValues.SetValues(Device);
            await _context.SaveChangesAsync();
            await UploadCodeToPicoAsync(Device);

            return Page();
        }


        public async Task<IActionResult> OnPostRestartAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null || device.Type != "Pico") return RedirectToPage();

            try {
                var client = new HttpClient();
                await client.GetAsync(device.Endpoint.TrimEnd('/') + "/reset");
            } catch {
                // ignorieren
            }

            return RedirectToPage(); // bleibt auf Edit-Seite
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null) return NotFound();

            _context.SmartDevices.Remove(device);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }

        private async Task UploadCodeToPicoAsync(SmartDevice device) {
            if (device.Type != "Pico" || string.IsNullOrWhiteSpace(device.Code)) return;

            try {
                using var http = new HttpClient();
                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(device.Code));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                content.Add(fileContent, "file", "main.py");

                var url = device.Endpoint.TrimEnd('/') + "/upload";
                await http.PostAsync(url, content);
            } catch {
                // Ignorieren
            }
        }
    }
}
