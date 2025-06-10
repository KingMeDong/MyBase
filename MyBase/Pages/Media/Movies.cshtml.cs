using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyBase.Pages.Media {
    public class MoviesModel : PageModel {
        public List<FileInfo> VideoFiles { get; set; }

        public void OnGet() {
            string moviesPath = "D:\\Filme";

            var allowedExtensions = new[] { ".mp4", ".mkv", ".avi", ".webm" };

            DirectoryInfo dir = new DirectoryInfo(moviesPath);

            if (dir.Exists) {
                VideoFiles = dir.GetFiles()
                    .Where(f => allowedExtensions.Contains(f.Extension.ToLower()))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
            } else {
                VideoFiles = new List<FileInfo>();
            }
        }
    }
}
