using Microsoft.AspNetCore.Mvc;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MyBase.Data;

namespace MyBase.Controllers {
    [ApiController]
    [Route("api/media")]
    public class MediaApiController : ControllerBase {
        // GET /api/media/images?page=1&pageSize=60&sort=taken_desc
        [HttpGet("images")]
        public IActionResult ListImages(int page = 1, int pageSize = 60, string sort = "taken_desc") {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 10, 200);

            var dir = FileHelper.ImagesDirectory;
            if (!System.IO.Directory.Exists(dir))
                return Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize, hasMore = false });

            var all = System.IO.Directory.GetFiles(dir)
                        .Select(p => new {
                            FileName = global::System.IO.Path.GetFileName(p),
                            FullPath = p
                        })
                        .ToList();

            var total = all.Count;

            // Sortierung (EXIF lesen – nur die page, um IO zu sparen? -> wir sortieren global: nötig für korrekte Reihenfolge)
            // Für große Ordner: ggf. Caching der Metadaten in Erwägung ziehen.
            var enriched = all.Select(x => {
                DateTime uploadUtc = new System.IO.FileInfo(x.FullPath).CreationTimeUtc;
                DateTime? takenUtc = TryGetTakenDateUtc(x.FullPath);
                return new { x.FileName, UploadDateUtc = uploadUtc, TakenDateUtc = takenUtc };
            });

            enriched = sort switch {
                "taken_asc" => enriched.OrderBy(i => i.TakenDateUtc ?? i.UploadDateUtc),
                "taken_desc" => enriched.OrderByDescending(i => i.TakenDateUtc ?? i.UploadDateUtc),
                "upload_asc" => enriched.OrderBy(i => i.UploadDateUtc),
                "upload_desc" => enriched.OrderByDescending(i => i.UploadDateUtc),
                "name_desc" => enriched.OrderByDescending(i => i.FileName, StringComparer.OrdinalIgnoreCase),
                "name_asc" => enriched.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase),
                _ => enriched.OrderByDescending(i => i.TakenDateUtc ?? i.UploadDateUtc),
            };

            var skip = (page - 1) * pageSize;
            var pageItems = enriched.Skip(skip).Take(pageSize)
                                    .Select(i => new {
                                        fileName = i.FileName,
                                        uploadDateUtc = i.UploadDateUtc,
                                        takenDateUtc = i.TakenDateUtc
                                    })
                                    .ToList();

            var hasMore = skip + pageItems.Count < total;
            return Ok(new { items = pageItems, total, page, pageSize, hasMore });
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
    }
}
