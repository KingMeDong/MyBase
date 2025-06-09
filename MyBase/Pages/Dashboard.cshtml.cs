using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MyBase.Models; // NEU → SystemStatusModel einbinden!

namespace MyBase.Pages {
    public class DashboardModel : PageModel {
        public SystemStatusModel SystemStatus { get; set; }

        private readonly IConfiguration _configuration;

        public DashboardModel(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void OnGet() {
            SystemStatus = new SystemStatusModel(_configuration);

            ViewData["DisplayName"] = HttpContext.Session.GetString("DisplayName");
        }

        // FormatBytes Helper → für schöne Anzeige
        public static string FormatBytes(long bytes) {
            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
