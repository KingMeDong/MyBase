using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyBase.Pages.Account {
    public class LogoutModel : PageModel {
        public IActionResult OnGet() {
            // Session leeren
            HttpContext.Session.Clear();

            // Redirect zu Login
            return RedirectToPage("/Account/Login");
        }
    }
}
