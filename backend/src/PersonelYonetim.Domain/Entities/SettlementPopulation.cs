using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Yıllık nüfus kaydı. Güncelleme eski yılı silmez.
/// Kaynak örneği: TÜİK ADNKS (isOfficial=true) veya LOCAL_DEV_SAMPLE.
/// </summary>
public class SettlementPopulation : AuditableEntity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;

    public int Year { get; set; }
    public int Population { get; set; }

    public int? MaleCount { get; set; }
    public int? FemaleCount { get; set; }
    public int? ChildCount { get; set; }

    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public bool IsOfficial { get; set; }
}
