using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MyBase.Clients;
using System.Threading.Tasks;

namespace MyBase.Pages.SmartHome {
    public class IndexModel : PageModel {
        private readonly IoBrokerClient _ioBrokerClient;

        public IndexModel(IoBrokerClient ioBrokerClient) {
            _ioBrokerClient = ioBrokerClient;
        }

        public string? AdminAlive { get; set; }
        public string? FreeMem { get; set; }

        // 🆕 Schaltbarer State → Platzhalter für späteres echtes Gerät
        public string? DemoSwitchState { get; set; }

        // StateId für die Schaltbare Card → später einfach anpassen!
        private const string DemoSwitchStateId = "system.adapter.admin.0.alive";

        public async Task OnGetAsync() {
            // Admin Adapter alive
            AdminAlive = await _ioBrokerClient.GetStateAsync("system.adapter.admin.0.alive");

            // Freier RAM in MB
            FreeMem = await _ioBrokerClient.GetStateAsync("system.host.raspberrypi.freemem");

            // 🆕 Schaltbarer State holen
            DemoSwitchState = await _ioBrokerClient.GetStateAsync(DemoSwitchStateId);
        }

        // 🆕 POST-Handler → Toggle Button gedrückt
        public async Task<IActionResult> OnPostToggleDemoSwitchAsync() {
            var currentState = await _ioBrokerClient.GetStateAsync(DemoSwitchStateId);

            // Toggle Logik
            bool newState = currentState?.ToLower() != "true";

            // Setzen
            await _ioBrokerClient.SetStateAsync(DemoSwitchStateId, newState);

            // Redirect → Seite neu laden (damit OnGetAsync wieder ausgeführt wird)
            return RedirectToPage();
        }
    }
}
