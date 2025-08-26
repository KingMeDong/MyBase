using Microsoft.AspNetCore.Mvc;
using MyBase.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace MyBase.Controllers {
    [ApiController]
    [Route("thumbs")]
    public class ThumbnailController : ControllerBase {
        // GET /thumbs/{fileName}?w=320&h=0
        [HttpGet("{fileName}")]
        public async Task<IActionResult> Get(string fileName, int w = 320, int h = 0) {
            var safe = global::System.IO.Path.GetFileName(fileName);
            var originalPath = FileHelper.GetImagePath(safe);

            if (!System.IO.File.Exists(originalPath))
                return NotFound();

            // Zielgröße „einhegen“
            w = Math.Clamp(w, 32, 2000);
            h = h <= 0 ? 0 : Math.Clamp(h, 32, 2000);

            FileHelper.EnsureImagesThumbCacheDirectoryExists();
            var cachedPath = FileHelper.GetImageThumbCachePath(safe, w, h);

            // Cache-Hit? (und nicht älter als das Original)
            if (System.IO.File.Exists(cachedPath)) {
                var cacheInfo = new FileInfo(cachedPath);
                var origInfo = new FileInfo(originalPath);
                if (cacheInfo.LastWriteTimeUtc >= origInfo.LastWriteTimeUtc) {
                    return PhysicalFile(cachedPath, "image/jpeg");
                }
            }

            // Neu erzeugen + in Cache schreiben
            using var image = await Image.LoadAsync(originalPath);
            image.Mutate(x => {
                x.AutoOrient(); // berücksichtigt EXIF-Rotation
                var size = new Size(w, h == 0 ? w : h);
                x.Resize(new ResizeOptions {
                    Size = size,
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                });
            });

            // JPEG (80 Qualität) in Cache ablegen
            var enc = new JpegEncoder { Quality = 80 };
            await using (var fs = new FileStream(cachedPath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                await image.SaveAsync(fs, enc);
            }

            return PhysicalFile(cachedPath, "image/jpeg");
        }
    }
}
