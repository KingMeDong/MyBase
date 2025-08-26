using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using MyBase.Data;

namespace MyBase.Controllers {
    [Route("download")]
    public class DownloadController : Controller {
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        // GET /download/{fileName}?scope=images
        [HttpGet("{fileName}")]
        public IActionResult Get(string fileName, [FromQuery] string? scope = null) {
            // Sicherheit: nur Dateiname (kein Pfad-Traversal)
            var safeName = global::System.IO.Path.GetFileName(fileName);

            // Verzeichnis je nach scope (default = Files)
            string baseDir = (scope != null && scope.Equals("images", System.StringComparison.OrdinalIgnoreCase))
                ? FileHelper.ImagesDirectory
                : FileHelper.UploadDirectory;

            var fullPath = global::System.IO.Path.Combine(baseDir, safeName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            // MIME-Type bestimmen
            if (!_contentTypeProvider.TryGetContentType(safeName, out var mimeType))
                mimeType = "application/octet-stream"; // Fallback

            // Datei zurückliefern, Range-Downloads erlaubt
            return PhysicalFile(fullPath, mimeType, safeName, enableRangeProcessing: true);
        }
    }
}
