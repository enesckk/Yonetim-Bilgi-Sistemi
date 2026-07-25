namespace PersonelYonetim.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Boşsa ContentRoot/App_Data/uploads kullanılır.</summary>
    public string? RootPath { get; set; }

    /// <summary>Tek dosya üst sınırı (bayt). Varsayılan 5 MB.</summary>
    public long MaxBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".docx"];
}
