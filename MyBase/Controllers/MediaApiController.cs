using Microsoft.AspNetCore.Mvc;
using MyBase.Data;

namespace MyBase.Controllers {
    [ApiController]
    [Route("api/media")]
    public class MediaApiController : ControllerBase {
        [HttpGet("images")]
        public IActionResult ListImages(int page = 1, int pageSize = 60) {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 10, 200);

            var dir = FileHelper.ImagesDirectory;
            if (!System.IO.Directory.Exists(dir))
                return Ok(new { items = Array.Empty<string>(), total = 0, page, pageSize, hasMore = false });

            var all = System.IO.Directory.GetFiles(dir)
                        .Select(p => global::System.IO.Path.GetFileName(p))
                        .OrderBy(n => n)
                        .ToList();

            var total = all.Count;
            var skip = (page - 1) * pageSize;
            var items = (skip >= total) ? new List<string>() : all.Skip(skip).Take(pageSize).ToList();
            var hasMore = skip + items.Count < total;

            return Ok(new { items, total, page, pageSize, hasMore });
        }
    }
}
