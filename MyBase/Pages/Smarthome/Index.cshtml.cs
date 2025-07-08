using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Clients;
using MyBase.Models;
using MyBase.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MyBase.Pages.SmartHome {
    public class IndexModel : PageModel {
        private readonly IoBrokerClient _ioBrokerClient;
        private readonly AppDbContext _context;

        public List<SmartDevice> Devices { get; set; } = new();
        public Dictionary<string, List<SmartDevice>> DevicesByRoom { get; set; } = new();

        public Dictionary<int, string> PicoStates { get; set; } = new();
        public Dictionary<int, string> ZigbeeStates { get; set; } = new();

        public string? AdminAlive { get; set; }
        public string? FreeMem { get; set; }

        [BindProperty]
        public int Value { get; set; }

        [BindProperty]
        public string HexColor { get; set; } = "#000000";

        [BindProperty]
        public string Endpoint { get; set; } = "";

        public IndexModel(AppDbContext context, IoBrokerClient ioBrokerClient) {
            _context = context;
            _ioBrokerClient = ioBrokerClient;
        }

        public async Task OnGetAsync() {
            AdminAlive = await _ioBrokerClient.GetStateAsync("system.adapter.admin.0.alive");
            FreeMem = await _ioBrokerClient.GetStateAsync("system.host.raspberrypi.freemem");

            var allDevices = await _context.SmartDevices.ToListAsync();
            var filtered = new List<SmartDevice>();
            var httpClient = new HttpClient();

            foreach (var device in allDevices) {
                bool erreichbar = false;

                if (device.Type == "Pico") {
                    try {
                        var pingUrl = device.Endpoint.TrimEnd('/') + "/status";
                        var response = await httpClient.GetAsync(pingUrl);
                        erreichbar = response.IsSuccessStatusCode;

                        if (device.ControlType == "switch") {
                            var status = await response.Content.ReadAsStringAsync();
                            PicoStates[device.Id] = status.Trim().ToLower();
                        }
                    } catch {
                        erreichbar = false;
                    }
                } else if (device.Type == "Zigbee") {
                    try {
                        var state = await _ioBrokerClient.GetStateAsync(device.Endpoint);
                        if (state != null) {
                            erreichbar = true;

                            if (device.ControlType == "switch") {
                                ZigbeeStates[device.Id] = state.Trim().ToLower();
                            }
                        }
                    } catch {
                        erreichbar = false;
                    }
                }

                if (erreichbar) {
                    filtered.Add(device);
                }
            }

            //Devices = filtered;
            DevicesByRoom = filtered
            .GroupBy(d => d.Room?.Name ?? "Unbekannt")
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task<IActionResult> OnPostToggleDeviceAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null) return RedirectToPage();

            if (device.Type == "Pico" && device.ControlType == "switch") {
                try {
                    var http = new HttpClient();
                    var statusUrl = device.Endpoint.TrimEnd('/') + "/status";
                    var current = await http.GetStringAsync(statusUrl);
                    var target = current.Trim().ToLower() == "on" ? "/off" : "/on";
                    await http.GetAsync(device.Endpoint.TrimEnd('/') + target);
                } catch { }
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleZigbeeAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null || string.IsNullOrWhiteSpace(device.Endpoint)) return RedirectToPage();

            try {
                var state = await _ioBrokerClient.GetStateAsync(device.Endpoint);
                var current = state?.Trim().ToLower();

                var newValue = current == "true" ? "false" : "true";
                await _ioBrokerClient.SetStateAsync(device.Endpoint, newValue);
            } catch {
                // Fehler ignorieren oder loggen
            }

            return RedirectToPage();
        }



        public async Task<IActionResult> OnPostSetSliderAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null) return RedirectToPage();

            if (device.Type == "Pico" && device.ControlType == "slider") {
                try {
                    var url = $"{device.Endpoint.TrimEnd('/')}/set?value={Value}";
                    await new HttpClient().GetAsync(url);
                } catch { }
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetColorAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null) return RedirectToPage();

            if (device.Type == "Pico" && device.ControlType == "rgb") {
                try {
                    var color = System.Drawing.ColorTranslator.FromHtml(HexColor);
                    var url = $"{device.Endpoint.TrimEnd('/')}/setcolor?r={color.R}&g={color.G}&b={color.B}";
                    await new HttpClient().GetAsync(url);
                } catch { }
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTriggerPicoAsync(int id) {
            var device = await _context.SmartDevices.FindAsync(id);
            if (device == null || string.IsNullOrWhiteSpace(Endpoint)) return RedirectToPage();

            if (device.Type == "Pico") {
                try {
                    var url = $"{device.Endpoint.TrimEnd('/')}{Endpoint}";
                    await new HttpClient().GetAsync(url);
                } catch { }
            }

            return RedirectToPage();
        }
    }
}
