namespace PersonelYonetim.Application.Features.Import;

/// <summary>
/// Excel toplu personel aktarımı.
/// Öğrenme noktası: satır bazlı sonuç — bir hata tüm dosyayı iptal etmez (partial success).
/// </summary>
public interface IEmployeeImportService
{
    /// <summary>Boş şablon + referans sayfaları (birim/unvan listesi).</summary>
    Task<ImportTemplateFile> BuildTemplateAsync(CancellationToken cancellationToken = default);

    Task<EmployeeImportResultDto> ImportAsync(
        Stream excelStream,
        string fileName,
        CancellationToken cancellationToken = default);
}

public sealed class ImportTemplateFile
{
    public required byte[] Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed class EmployeeImportResultDto
{
    public int TotalRows { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<EmployeeImportRowResultDto> Rows { get; init; } = [];
}

public sealed class EmployeeImportRowResultDto
{
    public int RowNumber { get; init; }
    public bool Success { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? FullName { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
