using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBase.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyBase.Pages.Files {
    public class IndexModel : PageModel {
        public List<string> UploadedFiles { get; set; } = new();

        [BindProperty]
        public List<IFormFile> FilesToUpload { get; set; } = new();

        public void OnGet() {
            FileHelper.EnsureUploadDirectoryExists();
            UploadedFiles = Directory.GetFiles(FileHelper.UploadDirectory)
                                     .Select(Path.GetFileName)
                                     .ToList();
        }

        public async Task<IActionResult> OnPostUploadAsync() {
            FileHelper.EnsureUploadDirectoryExists();

            if (FilesToUpload != null && FilesToUpload.Count > 0) {
                foreach (var file in FilesToUpload.Where(f => f != null && f.Length > 0)) {
                    var safeName = Path.GetFileName(file.FileName);
                    var filePath = FileHelper.GetFilePath(safeName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                }
            }

            // Bei AJAX: JSON zurück
            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest") {
                return new JsonResult(new { ok = true, uploaded = FilesToUpload?.Count ?? 0 });
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string fileName) {
            if (!string.IsNullOrWhiteSpace(fileName)) {
                var filePath = FileHelper.GetFilePath(fileName);
                if (System.IO.File.Exists(filePath)) {
                    System.IO.File.Delete(filePath);
                }
            }

            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest") {
                return new JsonResult(new { ok = true, deleted = fileName });
            }

            return RedirectToPage();
        }
    }
}
