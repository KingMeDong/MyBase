using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBase.Clients;
using MyBase.Models;
using MyBase.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyBase.Pages.SmartHome {
    public class IndexModel : PageModel {
        private readonly IoBrokerClient _ioBrokerClient;
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context, IoBrokerClient ioBrokerClient) {
            _context = context;
            _ioBrokerClient = ioBrokerClient;
        }

        public string? AdminAlive { get; set; }
        public string? FreeMem { get; set; }

        public List<SmartDevice> Devices { get; set; } = new();

        public async Task OnGetAsync() {
            AdminAlive = await _ioBrokerClient.GetStateAsync("system.adapter.admin.0.alive");
            FreeMem = await _ioBrokerClient.GetStateAsync("system.host.raspberrypi.freemem");

            Devices = await _context.SmartDevices.ToListAsync();
        }
    }
}
