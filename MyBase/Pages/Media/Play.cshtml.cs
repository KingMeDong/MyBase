using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MyBase.Pages.Media {
    public class PlayModel : PageModel {
        public string CurrentPath { get; set; }
        public string EncodedPath { get; set; }
        public string FileName { get; set; }
        public string ReturnPath { get; set; }

        public void OnGet(string path) {
            CurrentPath = WebUtility.UrlDecode(path);
            EncodedPath = WebUtility.UrlEncode(CurrentPath);

            FileName = Path.GetFileName(CurrentPath);
            ReturnPath = Path.GetDirectoryName(CurrentPath);
        }

        public string GetEncodedPath(string fullPath) {
            return WebUtility.UrlEncode(fullPath);
        }
    }
}
