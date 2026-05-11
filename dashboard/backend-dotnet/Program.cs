using DashboardApi.Middleware;
using DashboardApi.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using System.IO.Compression;
using System.Net.Sockets;

// Trigger dotnet watch restart
var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.AddSingleton<ConfigStore>(sp =>
{
    // Config.json lives at dashboard/data/config.json relative to backend-dotnet
    var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "data", "config.json"));
    // Also try relative to working directory
    var altPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "config.json"));
    return new ConfigStore(File.Exists(basePath) ? basePath : altPath);
});

// CORS — defaults cover the standard backend (5172) and Vite dev (5173) ports.
// start.ps1 overrides CORS_ORIGINS based on the actual configured ports; this
// fallback is for IDE-launched runs (dotnet run, Visual Studio) and tests.
var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")
    ?? "http://localhost:5172,http://127.0.0.1:5172,http://localhost:5173,http://127.0.0.1:5173";
var allowedOrigins = corsOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .Where(o => Uri.TryCreate(o, UriKind.Absolute, out var uri) &&
                (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1"))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var allowedOriginsSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .WithHeaders("Content-Type", "Accept", "X-API-Key");
    });
});

var app = builder.Build();

// --- Middleware pipeline (ORDER MATTERS — matches Python main.py) ---
// 1. Rate limiting (outermost — runs first on request, last on response)
app.UseMiddleware<RateLimitMiddleware>();
// 2. PAT scope exception handler (catches permission errors from any controller)
app.UseMiddleware<PatScopeExceptionMiddleware>();
// 3. API key authentication
app.UseMiddleware<ApiKeyMiddleware>();
// 4. CSRF origin validation
app.UseMiddleware<CsrfOriginMiddleware>(allowedOriginsSet);
// 5. Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();
// 6. Body size limit
app.UseMiddleware<BodySizeLimitMiddleware>();

app.UseResponseCompression();
app.UseCors();
app.MapControllers();

// Serve the built Vue SPA if dist/ exists
var distPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "dashboard", "frontend", "dist"));
// Fallback: when CWD is the backend-dotnet folder itself (e.g. dotnet run without --project)
if (!Directory.Exists(distPath))
    distPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "dist"));
if (Directory.Exists(distPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(distPath),
        RequestPath = "",
    });

    // Serve Documentation/ HTML files when present
    var docsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Documentation"));
    if (!Directory.Exists(docsPath))
        docsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Documentation"));
    if (Directory.Exists(docsPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(docsPath),
            RequestPath = "/Documentation",
        });
    }

    // Return JSON 404 for unknown API paths instead of falling back to SPA HTML.
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"detail\":\"Not found.\"}");
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(distPath, "index.html"));
    });
}

try
{
    app.Run();
}
catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
{
    Console.Error.WriteLine($"Failed to start: {ex.Message}");
    Console.Error.WriteLine("Another application is already using this port. Close the other application or use a different port (e.g. -BackendPort in start.ps1).");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to start: {ex.Message}");
    Environment.Exit(1);
}
