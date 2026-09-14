using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Files;

/// <summary>
/// Disk tabanlı depolama — wwwroot'a KOYMAZ (statik URL ile herkese açık olmasın).
/// Erişim yalnızca yetkili API endpoint'leri üzerinden.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly string _root;

    public LocalFileStorageService(IOptions<FileStorageOptions> options, IHostEnvironment env)
    {
        _options = options.Value;
        if (!env.IsDevelopment() && OperatingSystem.IsLinux() &&
            (string.IsNullOrWhiteSpace(_options.RootPath) || !Path.IsPathFullyQualified(_options.RootPath)))
            throw new InvalidOperationException("FileStorage:RootPath Production Linux ortamında mutlak ve kalıcı bir yol olmalıdır.");
        _root = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "uploads"))
            : Path.GetFullPath(_options.RootPath);

        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string relativeFolder,
        CancellationToken cancellationToken = default)
    {
        if (content.CanSeek && content.Length > _options.MaxBytes)
            throw CreateSizeError();

        var ext = NormalizeExtension(Path.GetExtension(originalFileName));
        if (!_options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["file"] =
                [
                    $"İzin verilen uzantılar: {string.Join(", ", _options.AllowedExtensions)}"
                ]
            });
        }

        // Magic-byte kontrolü: uzantı sahteciliğine karşı
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length > _options.MaxBytes)
            throw CreateSizeError();
        if (buffered.Length == 0)
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Dosya boş olamaz."]
            });
        }

        buffered.Position = 0;
        if (!FileSignatureValidator.Matches(buffered, ext))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Dosya içeriği uzantısıyla uyuşmuyor (güvenlik kontrolü)."]
            });
        }

        var safeFolder = SanitizeRelativeFolder(relativeFolder);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = $"{safeFolder}/{fileName}".Replace('\\', '/');
        var fullPath = ResolveUnderRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        buffered.Position = 0;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await buffered.CopyToAsync(fs, cancellationToken);
        }

        return new StoredFileResult
        {
            RelativePath = relativePath,
            Extension = ext,
            SizeBytes = buffered.Length
        };
    }

    public Task<StoredFileOpenResult?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.FromResult<StoredFileOpenResult?>(null);

        string fullPath;
        try
        {
            fullPath = ResolveUnderRoot(relativePath);
        }
        catch (ForbiddenException)
        {
            return Task.FromResult<StoredFileOpenResult?>(null);
        }

        if (!File.Exists(fullPath))
            return Task.FromResult<StoredFileOpenResult?>(null);

        var ext = NormalizeExtension(Path.GetExtension(fullPath));
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StoredFileOpenResult?>(new StoredFileOpenResult
        {
            Stream = stream,
            ContentType = ContentTypes.ForExtension(ext),
            DownloadFileName = Path.GetFileName(fullPath)
        });
    }

    public Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.CompletedTask;

        try
        {
            var fullPath = ResolveUnderRoot(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (ForbiddenException)
        {
            // Path traversal denemesi — sessizce yok say (çağıran 404 verebilir)
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Path traversal koruması: çözülen yol mutlaka kök altında olmalı.
    /// </summary>
    private string ResolveUnderRoot(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized))
            throw new ForbiddenException("Geçersiz dosya yolu.");

        var combined = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSep = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, _root, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Geçersiz dosya yolu.");

        return combined;
    }

    private static string SanitizeRelativeFolder(string relativeFolder)
    {
        var parts = relativeFolder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p is not "." and not ".." && p.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .ToArray();

        if (parts.Length == 0)
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Geçersiz depolama klasörü."]
            });

        return string.Join('/', parts);
    }

    private static string NormalizeExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return string.Empty;
        return ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
    }

    private AppValidationException CreateSizeError() =>
        new(new Dictionary<string, string[]>
        {
            ["file"] = [$"Dosya en fazla {_options.MaxBytes / (1024 * 1024)} MB olabilir."]
        });
}

internal static class ContentTypes
{
    public static string ForExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}

/// <summary>
/// Uzantı ↔ dosya imzası (magic bytes). Sadece uzantıya güvenmek yeterli değildir.
/// </summary>
internal static class FileSignatureValidator
{
    public static bool Matches(Stream stream, string ext)
    {
        if (!stream.CanSeek)
            return false;

        Span<byte> header = stackalloc byte[8];
        var pos = stream.Position;
        var read = stream.Read(header);
        stream.Position = pos;
        if (read < 4)
            return false;

        return ext.ToLowerInvariant() switch
        {
            ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46, // %PDF
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            // DOCX = ZIP konteyneri
            ".docx" => header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07),
            _ => false
        };
    }
}
