using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Data;
using System.IO;
using System.Linq;

namespace MyBase.Pages.Media {
    // gleiche Limits wie Files (optional)
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public class ImagesModel : PageModel {
        public List<string> ImageFiles { get; set; } = new();

        // gleicher Property-Name wie im Files-Manager, damit uploader.js nichts ändern muss
        [BindProperty]
        public List<IFormFile> FilesToUpload { get; set; } = new();

        public void OnGet() {
            FileHelper.EnsureImageDirectoryExists();
            ImageFiles = Directory.GetFiles(FileHelper.ImagesDirectory)
                                  .Select(Path.GetFileName)
                                  .OrderBy(n => n)
                                  .ToList();
        }

        // gleicher Handler-Name und -Signatur wie bei Files
        public async Task<IActionResult> OnPostUploadAsync() {
            FileHelper.EnsureImageDirectoryExists();

            if (FilesToUpload is { Count: > 0 }) {
                foreach (var file in FilesToUpload.Where(f => f != null && f.Length > 0)) {
                    var safeName = Path.GetFileName(file.FileName);
                    var target = FileHelper.GetImagePath(safeName);
                    using var fs = new FileStream(target, FileMode.Create);
                    await file.CopyToAsync(fs);
                }
            }

            // gleiche AJAX-Antwort wie Files
            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest")
                return new JsonResult(new { ok = true, uploaded = FilesToUpload?.Count ?? 0 });

            return RedirectToPage();
        }

        public IActionResult OnPostDelete(string fileName) {
            if (!string.IsNullOrWhiteSpace(fileName)) {
                var path = FileHelper.GetImagePath(fileName);
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }

            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest")
                return new JsonResult(new { ok = true, deleted = fileName });

            return RedirectToPage();
        }
    }
}
