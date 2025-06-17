using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using MyBase.Data;
using System.IO;

namespace MyBase.Controllers {
    [Route("download")]
    public class DownloadController : Controller {
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        [HttpGet("{fileName}")]
        public IActionResult Get(string fileName) {
            var fullPath = FileHelper.GetFilePath(fileName);

            if (!System.IO.File.Exists(fullPath)) {
                return NotFound();
            }

            // MIME-Type ermitteln
            if (!_contentTypeProvider.TryGetContentType(fileName, out var mimeType)) {
                mimeType = "application/octet-stream"; // Fallback
            }

            return PhysicalFile(fullPath, mimeType, fileName);
        }
    }
}
