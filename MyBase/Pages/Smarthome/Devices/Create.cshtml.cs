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

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) return Page();

            _context.SmartDevices.Add(Device);
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

        private async Task UploadCodeToPicoAsync(SmartDevice device) {
            if (device.Type != "Pico" || string.IsNullOrWhiteSpace(device.Code)) return;

            try {
                var http = new HttpClient();
                var content = new MultipartFormDataContent();
                var codeBytes = System.Text.Encoding.UTF8.GetBytes(device.Code);
                var fileContent = new ByteArrayContent(codeBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

                content.Add(fileContent, "file", "main.py");

                var url = device.Endpoint.TrimEnd('/') + "/upload";
                var response = await http.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) {
                    // Optional: Log oder Fehlerbehandlung
                }
            } catch {
                // Optional: Fehlerbehandlung
            }
        }

    }
}
