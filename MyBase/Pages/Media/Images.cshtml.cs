using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MyBase.Data;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace MyBase.Pages.Media {
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public class ImagesModel : PageModel {
        private const int DefaultPageSize = 60;

        public class ImageInfo {
            public string FileName { get; set; } = "";
            public DateTime UploadDateUtc { get; set; }
            public DateTime? TakenDateUtc { get; set; }
        }

        public List<ImageInfo> Images { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        [Display(Name = "Sortieren nach")]
        public string Sort { get; set; } = "taken_desc"; // taken_desc|taken_asc|upload_desc|upload_asc|name_asc|name_desc

        public int TotalCount { get; set; }
        public int PageSize { get; set; } = DefaultPageSize;

        [BindProperty] public List<IFormFile> FilesToUpload { get; set; } = new();

        public void OnGet() {
            FileHelper.EnsureImageDirectoryExists();

            var files = global::System.IO.Directory.GetFiles(FileHelper.ImagesDirectory);
            TotalCount = files.Length;

            // ENRICH: Für korrekte erste Seite global anreichern + sortieren, DANN Take(PageSize)
            var needTaken = Sort.StartsWith("taken", StringComparison.OrdinalIgnoreCase);

            var enriched = files.Select(full => {
                var fi = new FileInfo(full);
                var uploadUtc = fi.CreationTimeUtc != DateTime.MinValue ? fi.CreationTimeUtc : fi.LastWriteTimeUtc;
                DateTime? takenUtc = needTaken ? TryGetTakenDateUtc(full) : null;

                return new ImageInfo {
                    FileName = global::System.IO.Path.GetFileName(full),
                    UploadDateUtc = uploadUtc,
                    TakenDateUtc = takenUtc
                };
            });

            Images = SortImages(enriched, Sort).Take(PageSize).ToList();
        }

        public async Task<IActionResult> OnPostUploadAsync() {
            FileHelper.EnsureImageDirectoryExists();

            var saved = new List<object>();

            if (FilesToUpload is { Count: > 0 }) {
                foreach (var file in FilesToUpload.Where(f => f is { Length: > 0 })) {
                    var safe = global::System.IO.Path.GetFileName(file.FileName);
                    var target = FileHelper.GetImagePath(safe);

                    await using var fs = new FileStream(target, FileMode.Create);
                    await file.CopyToAsync(fs);

                    var fi = new FileInfo(target);
                    var uploadUtc = fi.CreationTimeUtc != DateTime.MinValue ? fi.CreationTimeUtc : fi.LastWriteTimeUtc;
                    var takenUtc = TryGetTakenDateUtc(target);

                    saved.Add(new {
                        fileName = safe,
                        uploadDateUtc = uploadUtc,
                        takenDateUtc = takenUtc
                    });
                }
            }

            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest")
                return new JsonResult(new { ok = true, files = saved });

            return RedirectToPage(new { sort = Sort });
        }

        public IActionResult OnPostDelete(string fileName) {
            if (!string.IsNullOrWhiteSpace(fileName)) {
                var orig = FileHelper.GetImagePath(fileName);
                if (System.IO.File.Exists(orig)) System.IO.File.Delete(orig);
            }

            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) && xrw == "XMLHttpRequest")
                return new JsonResult(new { ok = true, deleted = fileName });

            return RedirectToPage(new { sort = Sort });
        }

        private static DateTime? TryGetTakenDateUtc(string fullPath) {
            try {
                var dirs = ImageMetadataReader.ReadMetadata(fullPath);
                var subIfd = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                    return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
            } catch { }
            return null;
        }

        private static List<ImageInfo> SortImages(IEnumerable<ImageInfo> list, string sort) {
            return sort switch {
                "taken_asc" => list.OrderBy(i => i.TakenDateUtc ?? i.UploadDateUtc).ToList(),
                "taken_desc" => list.OrderByDescending(i => i.TakenDateUtc ?? i.UploadDateUtc).ToList(),
                "upload_asc" => list.OrderBy(i => i.UploadDateUtc).ToList(),
                "upload_desc" => list.OrderByDescending(i => i.UploadDateUtc).ToList(),
                "name_desc" => list.OrderByDescending(i => i.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
                "name_asc" => list.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => list.OrderByDescending(i => i.TakenDateUtc ?? i.UploadDateUtc).ToList(),
            };
        }
    }
}
