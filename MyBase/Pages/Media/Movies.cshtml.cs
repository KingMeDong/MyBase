using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace MyBase.Pages.Media {
    public class MoviesModel : PageModel {
        private readonly IConfiguration _configuration;

        public MoviesModel(IConfiguration configuration) {
            _configuration = configuration;
        }

        public List<DirectoryInfo> SubDirectories { get; set; }
        public List<FileInfo> VideoFiles { get; set; }
        public string CurrentPath { get; set; }
        public string RelativePath { get; set; }
        public string RootPath { get; set; } // <== HINZUGEFÜGT

        public void OnGet(string path) {
            string driveLetter = _configuration["SystemSettings:StorageDriveLetter"] ?? "D";
            RootPath = $"{driveLetter}:\\Filme";  // <== RootPath wird hier definiert

            CurrentPath = string.IsNullOrEmpty(path) ? RootPath : path;
            RelativePath = CurrentPath.Replace(RootPath, "").TrimStart('\\');

            var allowedExtensions = new[] { ".mp4", ".mkv", ".avi", ".webm" };

            DirectoryInfo dir = new DirectoryInfo(CurrentPath);

            if (dir.Exists) {
                SubDirectories = dir.GetDirectories("*", SearchOption.TopDirectoryOnly)
                    .Where(d => (d.Attributes & FileAttributes.Hidden) == 0 &&
                                (d.Attributes & FileAttributes.System) == 0)
                    .OrderBy(d => d.Name)
                    .ToList();

                VideoFiles = dir.GetFiles()
                    .Where(f => allowedExtensions.Contains(f.Extension.ToLower()))
                    .OrderBy(f => f.Name)
                    .ToList();
            } else {
                SubDirectories = new List<DirectoryInfo>();
                VideoFiles = new List<FileInfo>();
            }
        }

        public string GetEncodedPath(string fullPath) {
            return System.Net.WebUtility.UrlEncode(fullPath);
        }
    }
}
