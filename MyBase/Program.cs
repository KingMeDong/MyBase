using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyBase.Data;
using MyBase.Models;
using MyBase.Services;

var builder = WebApplication.CreateBuilder(args);

// FileHelper initialisieren (für Pfade wie ImagesDirectory)
FileHelper.Initialize(builder.Configuration);

// Maximale Requestgröße (Kestrel) aufheben
builder.WebHost.ConfigureKestrel(k => {
    k.Limits.MaxRequestBodySize = null;
});

// Konfiguration laden
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Services
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddHostedService<ReminderService>();

// Für Download-/Thumbnail-APIs
builder.Services.AddControllers();

// Externe Clients
builder.Services.AddHttpClient<MyBase.Clients.IoBrokerClient>();

var app = builder.Build();

// Fehlerseite/HSTS
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Statische Dateien aus wwwroot
app.UseStaticFiles();

// 🔹 Statische Auslieferung der ORIGINAL-Bilder außerhalb von wwwroot
//    -> /media/images/<dateiname> zeigt Datei aus FileHelper.ImagesDirectory
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(FileHelper.ImagesDirectory),
    RequestPath = "/media/images"
});

app.UseRouting();

app.MapControllers();   // wichtig: aktiviert /download und /thumbs
app.UseSession();
app.UseAuthorization();

// Root auf Login umleiten
app.MapGet("/", context => {
    context.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

// (Bleibt: Dein Streaming-Endpunkt für Videos)
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

    string contentType = "application/octet-stream";
    var extension = Path.GetExtension(filePath).ToLowerInvariant();
    switch (extension) {
        case ".mp4": contentType = "video/mp4"; break;
        case ".webm": contentType = "video/webm"; break;
        case ".mkv": contentType = "video/x-matroska"; break;
        case ".avi": contentType = "video/x-msvideo"; break;
        default: contentType = "application/octet-stream"; break;
    }

    context.Response.Headers.Add("Accept-Ranges", "bytes");
    context.Response.ContentType = contentType;

    if (context.Request.Headers.ContainsKey("Range")) {
        var rangeHeader = context.Request.Headers["Range"].ToString(); // z.B. "bytes=0-999999"
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
