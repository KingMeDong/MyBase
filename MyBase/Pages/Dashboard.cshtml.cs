using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyBase.Pages
{
    public class DashboardModel : PageModel
    {
        public IActionResult OnGet() {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username"))) {
                // Nicht eingeloggt → zurück auf Login
                return RedirectToPage("/Account/Login");
            }

            // Optional: DisplayName in ViewData speichern
            ViewData["DisplayName"] = HttpContext.Session.GetString("DisplayName");

            return Page();
        }
    }
}
