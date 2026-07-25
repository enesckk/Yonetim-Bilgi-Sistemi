namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>
/// Yerel/uzaktan dosya deposu soyutlaması.
/// DB'de yalnızca göreli yol tutulur; fiziksel kök config'tedir.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Dosyayı kaydeder. relativeFolder örn. "special-conditions/{employeeId}".
    /// Dönüş: DB'ye yazılacak göreli yol (kök dahil değil).
    /// </summary>
    Task<StoredFileResult> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string relativeFolder,
        CancellationToken cancellationToken = default);

    Task<StoredFileOpenResult?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default);
}

public sealed class StoredFileResult
{
    public required string RelativePath { get; init; }
    public required string Extension { get; init; }
    public required long SizeBytes { get; init; }
}

public sealed class StoredFileOpenResult : IAsyncDisposable
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
