using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Data;
using MyBase.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyBase.Pages.SmartHome.Devices {
    public class IndexModel : PageModel {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context) {
            _context = context;
        }

        public List<SmartDevice> Devices { get; set; } = new();

        public async Task OnGetAsync() {
            Devices = await _context.SmartDevices.ToListAsync();
        }
    }
}
