using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyBase.Clients;
using MyBase.Models;
using MyBase.Data;

namespace MyBase.Pages.SmartHome;

public class WallModel : PageModel {
    private readonly IoBrokerClient _ioBrokerClient;
    private readonly AppDbContext _context;

    public List<SmartDevice> Devices { get; set; } = new();
    public Dictionary<string, List<SmartDevice>> DevicesByRoom { get; set; } = new();
    public Dictionary<int, string> PicoStates { get; set; } = new();
    public Dictionary<int, string> ZigbeeStates { get; set; } = new();

    [BindProperty]
    public int Value { get; set; }

    [BindProperty]
    public string HexColor { get; set; } = "#000000";

    [BindProperty]
    public string Endpoint { get; set; } = "";

    public WallModel(AppDbContext context, IoBrokerClient ioBrokerClient) {
        _context = context;
        _ioBrokerClient = ioBrokerClient;
    }

    public async Task OnGetAsync() {
        var allDevices = await _context.SmartDevices
            .Include(d => d.Room)
            .ToListAsync();

        var filtered = new List<SmartDevice>();
        var httpClient = new HttpClient();

        foreach (var device in allDevices) {
            bool erreichbar = false;

            if (device.Type == "Pico") {
                try {
                    var pingUrl = device.Endpoint.TrimEnd('/') + "/status";
                    var response = await httpClient.GetAsync(pingUrl);
                    erreichbar = response.IsSuccessStatusCode;

                    if (device.ControlType == "switch")
                        PicoStates[device.Id] = (await response.Content.ReadAsStringAsync()).Trim().ToLower();
                } catch { }
            } else if (device.Type == "Zigbee") {
                try {
                    var state = await _ioBrokerClient.GetStateAsync(device.Endpoint);
                    erreichbar = state != null;

                    if (device.ControlType == "switch")
                        ZigbeeStates[device.Id] = state?.Trim().ToLower() ?? "unbekannt";
                } catch { }
            }

            if (erreichbar)
                filtered.Add(device);
        }

        Devices = filtered;

        DevicesByRoom = Devices
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
        } catch { }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetZigbeeSliderAsync(int id) {
        var device = await _context.SmartDevices.FindAsync(id);
        if (device == null) return RedirectToPage();

        if (device.Type == "Zigbee" && device.ControlType == "slider") {
            try {
                await _ioBrokerClient.SetStateAsync(device.Endpoint, Value.ToString());
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

    public async Task<JsonResult> OnGetDeviceStatesAsync() {
        var picoStates = new Dictionary<int, string>();
        var zigbeeStates = new Dictionary<int, string>();

        var devices = await _context.SmartDevices.ToListAsync();
        var http = new HttpClient();

        foreach (var d in devices) {
            try {
                if (d.Type == "Pico" && d.ControlType == "switch") {
                    var statusUrl = d.Endpoint.TrimEnd('/') + "/status";
                    var state = await http.GetStringAsync(statusUrl);
                    picoStates[d.Id] = state.Trim().ToLower();
                } else if (d.Type == "Zigbee" && d.ControlType == "switch") {
                    var state = await _ioBrokerClient.GetStateAsync(d.Endpoint);
                    zigbeeStates[d.Id] = state?.Trim().ToLower() ?? "unbekannt";
                }
            } catch { }
        }

        return new JsonResult(new {
            pico = picoStates,
            zigbee = zigbeeStates
        });
    }


    // Post-Handler (ToggleDevice, ToggleZigbee, SetZigbeeSlider, SetColor)
    // werden automatisch aus IndexModel übernommen, da du sie zentral verwendest
}
