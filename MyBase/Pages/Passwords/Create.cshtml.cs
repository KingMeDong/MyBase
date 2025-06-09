using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Models;
using MyBase.Data;
using System;

namespace MyBase.Pages.Passwords {
    public class CreateModel : PageModel {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context) {
            _context = context;
        }

        [BindProperty]
        public PasswordEntry PasswordEntry { get; set; }

        public void OnGet() {
        }

        public IActionResult OnPost() {
            if (!ModelState.IsValid)
                return Page();

            // Benutzer-ID aus Session lesen
            int userId = int.Parse(HttpContext.Session.GetString("UserId"));

            PasswordEntry.UserId = userId;
            PasswordEntry.CreatedAt = DateTime.Now;
            PasswordEntry.UpdatedAt = DateTime.Now;

            _context.Passwords.Add(PasswordEntry);
            _context.SaveChanges();

            return RedirectToPage("Index");
        }
    }
}
