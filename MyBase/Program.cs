using Microsoft.EntityFrameworkCore;
using MyBase.Data; // hinzufügen
var builder = WebApplication.CreateBuilder(args);


builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSession();

// DbContext registrieren
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));








var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();


app.MapGet("/", context => {
    context.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});


app.MapGet("/Media/Stream", async (HttpContext context) => {
    var pathParam = context.Request.Query["path"];
    var filePath = System.Net.WebUtility.UrlDecode(pathParam);

    if (!System.IO.File.Exists(filePath)) {
        context.Response.StatusCode = 404;
        return;
    }

    var fileInfo = new System.IO.FileInfo(filePath);
    long totalLength = fileInfo.Length;
    long start = 0;
    long end = totalLength - 1;

    // Dynamischer ContentType
    string contentType = "application/octet-stream";
    var extension = Path.GetExtension(filePath).ToLowerInvariant();

    switch (extension) {
        case ".mp4":
            contentType = "video/mp4";
            break;
        case ".webm":
            contentType = "video/webm";
            break;
        case ".mkv":
            contentType = "video/x-matroska";
            break;
        case ".avi":
            contentType = "video/x-msvideo";
            break;
        default:
            contentType = "application/octet-stream";
            break;
    }

    context.Response.Headers.Add("Accept-Ranges", "bytes");
    context.Response.ContentType = contentType;

    if (context.Request.Headers.ContainsKey("Range")) {
        var rangeHeader = context.Request.Headers["Range"].ToString();
        // Beispiel-Format: Range: bytes=0-999999
        var range = rangeHeader.Replace("bytes=", "").Split('-');
        start = long.Parse(range[0]);
        if (range.Length > 1 && !string.IsNullOrEmpty(range[1])) {
            end = long.Parse(range[1]);
        }

        context.Response.StatusCode = 206; // Partial Content
        context.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{totalLength}");
    }

    long contentLength = end - start + 1;
    context.Response.ContentLength = contentLength;

    using var stream = System.IO.File.OpenRead(filePath);
    stream.Seek(start, SeekOrigin.Begin);

    byte[] buffer = new byte[64 * 1024]; // 64 KB Buffer
    long remaining = contentLength;

    while (remaining > 0) {
        int toRead = (int)Math.Min(buffer.Length, remaining);
        int read = await stream.ReadAsync(buffer, 0, toRead);
        if (read == 0) break;

        await context.Response.Body.WriteAsync(buffer, 0, read);
        remaining -= read;
    }
});





app.MapRazorPages();

app.Run();
