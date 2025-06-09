using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Data;
using MyBase.Models;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace MyBase.Pages.Account {
    public class LoginModel : PageModel {
        private readonly AppDbContext _db;

        public LoginModel(AppDbContext db) {
            _db = db;
        }

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet() {
        }

        public IActionResult OnPost() {
            var user = _db.Users.FirstOrDefault(u => u.Username == Username);

            if (user != null && user.PasswordHash == Password) // später Hash vergleichen!
            {
                // Session setzen
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("DisplayName", user.DisplayName ?? user.Username);

                return RedirectToPage("/Dashboard");

            } else {
                ErrorMessage = "Ungültiger Benutzername oder Passwort.";
                return Page();
            }
        }
    }
}
