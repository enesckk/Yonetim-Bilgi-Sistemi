using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PersonelYonetim.Application.Common.Interfaces;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Files;

/// <summary>Private Supabase bucket. The secret key is used by the server only.</summary>
public sealed class SupabaseFileStorageService : IFileStorageService
{
    private readonly HttpClient _http;
    private readonly FileStorageOptions _files;
    private readonly Uri _baseUrl;
    private readonly string _bucket;
    private readonly string _secret;

    public SupabaseFileStorageService(HttpClient http, IOptions<SupabaseStorageOptions> storage,
        IOptions<FileStorageOptions> files)
    {
        _http = http;
        _files = files.Value;
        var options = storage.Value;
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || !url.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
            url.Host.Length <= ".supabase.co".Length || url.Port != 443 || url.AbsolutePath != "/")
            throw new InvalidOperationException("Supabase:Url geçerli bir HTTPS proje adresi olmalıdır.");
        if (string.IsNullOrWhiteSpace(options.SecretKey) || !options.SecretKey.StartsWith("sb_secret_", StringComparison.Ordinal))
            throw new InvalidOperationException("Supabase:SecretKey yeni biçimde bir sunucu gizli anahtarı olmalıdır.");
        if (string.IsNullOrWhiteSpace(options.Bucket) ||
            !Regex.IsMatch(options.Bucket, "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,62}$"))
            throw new InvalidOperationException("Supabase:Bucket geçersiz.");
        _baseUrl = url;
        _bucket = options.Bucket;
        _secret = options.SecretKey;
    }

    public async Task<StoredFileResult> SaveAsync(Stream content, string originalFileName,
        string contentType, string relativeFolder, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!_files.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw FileError($"İzin verilen uzantılar: {string.Join(", ", _files.AllowedExtensions)}");
        if (content.CanSeek && content.Length > _files.MaxBytes)
            throw FileError($"Dosya en fazla {_files.MaxBytes / (1024 * 1024)} MB olabilir.");

        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await content.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > _files.MaxBytes)
                throw FileError($"Dosya en fazla {_files.MaxBytes / (1024 * 1024)} MB olabilir.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        if (buffer.Length == 0)
            throw FileError("Dosya boş olamaz.");
        buffer.Position = 0;
        if (!FileSignatureValidator.Matches(buffer, ext))
            throw FileError("Dosya içeriği uzantısıyla uyuşmuyor (güvenlik kontrolü).");

        var folder = NormalizeFolder(relativeFolder);
        var path = $"{folder}/{Guid.NewGuid():N}{ext}";
        using var request = NewRequest(HttpMethod.Post, ObjectUrl(path));
        request.Content = new ByteArrayContent(buffer.ToArray());
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentTypes.ForExtension(ext));
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new StoredFileResult { RelativePath = path, Extension = ext, SizeBytes = buffer.Length };
    }

    public async Task<StoredFileOpenResult?> OpenReadAsync(string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePath(relativePath, out var path))
            return null;
        using var request = NewRequest(HttpMethod.Get, ObjectUrl(path));
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new StoredFileOpenResult
        {
            Stream = new MemoryStream(bytes, writable: false),
            ContentType = ContentTypes.ForExtension(Path.GetExtension(path)),
            DownloadFileName = Path.GetFileName(path)
        };
    }

    public async Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePath(relativePath, out var path))
            return;
        using var request = NewRequest(HttpMethod.Delete, ObjectUrl(path));
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage NewRequest(HttpMethod method, Uri url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", _secret);
        return request;
    }

    private Uri ObjectUrl(string path) => new(_baseUrl,
        $"storage/v1/object/{Uri.EscapeDataString(_bucket)}/{EncodePath(path)}");

    private static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string NormalizeFolder(string folder)
    {
        if (!TryNormalizePath(folder, out var safePath))
            throw FileError("Geçersiz depolama klasörü.");
        return safePath;
    }

    private static bool TryNormalizePath(string? path, out string safePath)
    {
        safePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith('/') ||
            path.Split('/').Any(p => p is "" or "." or ".." ||
                !Regex.IsMatch(p, "^[a-zA-Z0-9._-]+$")))
            return false;
        safePath = path;
        return true;
    }

    private static AppValidationException FileError(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });
}

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Supabase";
    public string? Url { get; set; }
    public string? SecretKey { get; set; }
    public string? Bucket { get; set; }
}
