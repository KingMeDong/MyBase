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
        public IFormFile? UploadedFile { get; set; }
        public void OnGet() {
            FileHelper.EnsureUploadDirectoryExists();
            UploadedFiles = Directory.GetFiles(FileHelper.UploadDirectory)
                                     .Select(Path.GetFileName)
                                     .ToList();
        }


        public async Task<IActionResult> OnPostUploadAsync() {
            if (UploadedFile != null) {
                FileHelper.EnsureUploadDirectoryExists();
                var filePath = FileHelper.GetFilePath(Path.GetFileName(UploadedFile.FileName));
                using (var stream = new FileStream(filePath, FileMode.Create)) {
                    await UploadedFile.CopyToAsync(stream);
                }
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

            return RedirectToPage();
        }

    }




}
