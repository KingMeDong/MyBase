using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Models;
using MyBase.Data; // dein DbContext Namespace
using System.Collections.Generic;
using System.Linq;

namespace MyBase.Pages.Passwords {
    public class IndexModel : PageModel {
        private readonly AppDbContext _context;


        private int GetCurrentUserId() {
            // Session["UserId"] muss beim Login gesetzt werden!
            // hier als Beispiel:
            return int.Parse(HttpContext.Session.GetString("UserId"));
        }



        public IndexModel(AppDbContext context) {
            _context = context;
        }

        public List<PasswordEntry> Passwords { get; set; }

        public void OnGet() {
            int currentUserId = GetCurrentUserId(); // neue Methode!

            Passwords = _context.Passwords
                .Where(p => p.UserId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();
        }

    }
}
