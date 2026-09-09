using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Mahalledeki belediye açık alanı (park, meydan, spor sahası vb.).</summary>
public class SettlementArea : AuditableEntity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Park | Meydan | Spor alanı | Açık etkinlik alanı | Çocuk oyun alanı | Yeşil alan</summary>
    public string AreaType { get; set; } = "Park";
    public string? Note { get; set; }
    public string? Address { get; set; }
}
