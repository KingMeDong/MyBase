using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyBase.Data;
using MyBase.Models;
using MyBase.Models.Finance;
using MyBase.Services;
using MyBase.Services.MarketData;

var builder = WebApplication.CreateBuilder(args);

// FileHelper initialisieren
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

// CPAPI Options binden
builder.Services.Configure<CpapiOptions>(builder.Configuration.GetSection("CPAPI"));

// 🔹 Gemeinsamer Cookie-Container (wichtig für Session!)
var cpapiCookies = new CookieContainer();
builder.Services.AddSingleton(cpapiCookies);

// HttpClient für CPAPI (Gateway)
builder.Services.AddHttpClient("Cpapi", (sp, c) => {
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CpapiOptions>>().CurrentValue;
    c.BaseAddress = new Uri(opt.BaseUrl);
    c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MyBaseBot/1.0");
    c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
    c.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
})
.ConfigurePrimaryHttpMessageHandler(sp => {
    var cookies = sp.GetRequiredService<CookieContainer>();
    return new HttpClientHandler {
        CookieContainer = cookies,
        UseCookies = true,
        ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
    };
});

// 🔹 Background-Services
builder.Services.AddSingleton<SessionManager>(); // geteilter State
builder.Services.AddHostedService(sp => sp.GetRequiredService<SessionManager>());
builder.Services.AddHostedService<MarketDataService>();

var app = builder.Build();

// Fehlerseite/HSTS
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Statische Dateien aus wwwroot
app.UseStaticFiles();

// Statische Auslieferung der ORIGINAL-Bilder
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(FileHelper.ImagesDirectory),
    RequestPath = "/media/images"
});

app.UseRouting();

app.MapControllers();
app.UseSession();
app.UseAuthorization();

// Root auf Login umleiten
app.MapGet("/", context => {
    context.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

// Media Streaming
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
    }

    context.Response.Headers.Add("Accept-Ranges", "bytes");
    context.Response.ContentType = contentType;

    if (context.Request.Headers.ContainsKey("Range")) {
        var rangeHeader = context.Request.Headers["Range"].ToString();
        var range = rangeHeader.Replace("bytes=", "").Split('-');
        start = long.Parse(range[0]);
        if (range.Length > 1 && !string.IsNullOrEmpty(range[1])) {
            end = long.Parse(range[1]);
        }

        context.Response.StatusCode = 206;
        context.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{totalLength}");
    }

    long contentLength = end - start + 1;
    context.Response.ContentLength = contentLength;

    using var stream = System.IO.File.OpenRead(filePath);
    stream.Seek(start, SeekOrigin.Begin);

    byte[] buffer = new byte[64 * 1024];
    long remaining = contentLength;

    while (remaining > 0) {
        int toRead = (int)Math.Min(buffer.Length, remaining);
        int read = await stream.ReadAsync(buffer, 0, toRead);
        if (read == 0) break;

        await context.Response.Body.WriteAsync(buffer, 0, read);
        remaining -= read;
    }
});

// 🔹 Minimal-APIs für Gateway
app.MapPost("/api/gateway/start", async (AppDbContext db) => {
    var e = await db.AppSettings.FindAsync("GatewayDesiredState");
    if (e is null)
        db.AppSettings.Add(new AppSetting { Key = "GatewayDesiredState", Value = "Running" });
    else
        e.Value = "Running";
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true, desired = "Running" });
});

app.MapPost("/api/gateway/stop", async (AppDbContext db) => {
    var e = await db.AppSettings.FindAsync("GatewayDesiredState");
    if (e is null)
        db.AppSettings.Add(new AppSetting { Key = "GatewayDesiredState", Value = "Stopped" });
    else
        e.Value = "Stopped";
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true, desired = "Stopped" });
});

// --- MARKETDATA START/STOP/STATUS ---
app.MapPost("/api/marketdata/start", async (AppDbContext db) => {
    var e = await db.AppSettings.FindAsync("MarketDataDesiredState");
    if (e is null) db.AppSettings.Add(new AppSetting { Key = "MarketDataDesiredState", Value = "Running" });
    else e.Value = "Running";
    await db.SaveChangesAsync();

    SessionLogBuffer.Append("CMD: MarketDataDesiredState -> Running (POST)");
    return Results.Ok(new { ok = true, desired = "Running" });
});



app.MapPost("/api/marketdata/stop", async (AppDbContext db) => {
    var e = await db.AppSettings.FindAsync("MarketDataDesiredState");
    if (e is null) db.AppSettings.Add(new AppSetting { Key = "MarketDataDesiredState", Value = "Stopped" });
    else e.Value = "Stopped";
    await db.SaveChangesAsync();

    SessionLogBuffer.Append("CMD: MarketDataDesiredState -> Stopped (POST)");
    return Results.Ok(new { ok = true, desired = "Stopped" });
});




app.MapGet("/api/marketdata/status", async (AppDbContext db, SessionManager session) => {
    var desired = (await db.AppSettings.FindAsync("MarketDataDesiredState"))?.Value ?? "Stopped";
    var instId = await db.Instruments.Where(i => i.IsActive).Select(i => i.Id).OrderBy(i => i).FirstOrDefaultAsync();
    var fs = instId == 0 ? null : await db.FeedStates.FindAsync(instId);

    var dto = new MarketDataStatusDto(
        desired,
        session.State.ToString(),
        (fs?.Status switch { 1 => "Running", 2 => "Error", _ => "Stopped" }),
        fs?.LastHeartbeatUtc,
        fs?.LastRealtimeTsUtc,
        fs?.LastError
    );
    return Results.Ok(dto);
});
app.MapGet("/api/gateway/status", async (AppDbContext db, SessionManager session) => {
    var gw = (await db.AppSettings.FindAsync("GatewayDesiredState"))?.Value ?? "Running";
    return Results.Ok(new { desired = gw, session = session.State.ToString() });
});
// Rolling Log aus dem SessionManager holen
app.MapGet("/api/marketdata/log", () => {
    // SessionLogBuffer ist eine statische Utility-Klasse (siehe unten)
    return Results.Text(SessionLogBuffer.ReadAll(), "text/plain");
});


app.MapRazorPages();
app.Run();
