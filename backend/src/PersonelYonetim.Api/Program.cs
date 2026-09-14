using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using PersonelYonetim.Application;
using PersonelYonetim.Infrastructure;
using PersonelYonetim.Infrastructure.Persistence.Seed;
using PersonelYonetim.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "application/geo+json",
        "text/json",
        "application/javascript",
        "text/css",
        "text/plain",
        "image/svg+xml",
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Cookie (refresh) için zorunlu
    });
});

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseResponseCompression();
app.UseMiddleware<LookupCacheHeadersMiddleware>();
app.UseHttpsRedirection();

// Güvenlik başlıkları (XSS / clickjacking azaltma)
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    var isApi = context.Request.Path.StartsWithSegments("/api");
    context.Response.Headers.TryAdd("Content-Security-Policy", isApi
        ? "default-src 'none'; frame-ancestors 'none'"
        : "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob: https://*.tile.openstreetmap.org https://tile.openstreetmap.org; connect-src 'self'; worker-src 'self' blob:; manifest-src 'self'");
    await next();
});

app.UseDefaultFiles();
var staticContentTypes = new FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".geojson"] = "application/geo+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi().RequireAuthorization();

app.MapControllers();
app.MapGet("/geo/sehitkamil-boundary.geojson", () =>
    Results.File(Path.Combine(app.Environment.WebRootPath, "geo", "sehitkamil-boundary.geojson"), "application/geo+json"))
    .AllowAnonymous();
app.MapGet("/geo/sehitkamil-mahalleler.geojson", () =>
    Results.File(Path.Combine(app.Environment.WebRootPath, "geo", "sehitkamil-mahalleler.geojson"), "application/geo+json"))
    .AllowAnonymous();
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
