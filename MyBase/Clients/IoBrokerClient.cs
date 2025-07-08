using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyBase.Clients {
    public class IoBrokerClient {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public IoBrokerClient(HttpClient httpClient, IConfiguration configuration) {
            _httpClient = httpClient;
            _baseUrl = configuration["IoBroker:BaseUrl"] ?? "http://192.168.178.58:8087"; // Fallback
        }

        // Beispiel: GET Wert eines States
        public async Task<string?> GetStateAsync(string stateId) {
            var url = $"{_baseUrl}/get/{stateId}";

            try {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("val", out var val)) {
                    return val.ToString(); // ganz bewusst KEINE Umwandlung
                }
            } catch {
                return null;
            }

            return null;
        }



        
        public async Task<bool> SetStateAsync(string stateId, object value) {
            var url = $"{_baseUrl}/set/{stateId}?value={value}";

            try {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            } catch {
                return false;
            }
        }
    }
}
