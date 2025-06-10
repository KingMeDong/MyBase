using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyBase.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class SystemStatusController : ControllerBase {
        private static readonly PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private static readonly PerformanceCounter ramAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");

        [HttpGet]
        public IActionResult Get() {
            // CPU
            float cpuUsage = cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100); // Warten für realistischen Wert
            cpuUsage = cpuCounter.NextValue();

            // RAM
            float availableMemoryMB = ramAvailableCounter.NextValue();
            float totalMemoryMB = GetTotalMemoryInMB();

            float usedMemoryMB = totalMemoryMB - availableMemoryMB;
            float ramUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;

            return Ok(new {
                cpuUsage = Math.Round(cpuUsage, 1),
                ramUsage = Math.Round(ramUsagePercent, 1),
                usedMemoryMB = Math.Round(usedMemoryMB, 0),
                totalMemoryMB = Math.Round(totalMemoryMB, 0)
            });
        }

        private float GetTotalMemoryInMB() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // Nutze PerformanceCounter für total memory (Commit Limit entspricht oft total phys RAM)
                var commitLimitCounter = new PerformanceCounter("Memory", "Commit Limit");
                float totalMemoryBytes = commitLimitCounter.NextValue();
                float totalMemoryMB = totalMemoryBytes / 1024 / 1024;
                return totalMemoryMB;
            } else {
                // Linux/macOS → hier müsste man /proc/meminfo parsen oder andere Tools verwenden
                return 0; // Dummy Wert
            }
        }
    }
}
