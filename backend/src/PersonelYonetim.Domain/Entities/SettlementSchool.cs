using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

public class SettlementSchool : AuditableEntity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Anaokulu | İlkokul | Ortaokul | Lise | İmam Hatip | Diğer</summary>
    public string SchoolType { get; set; } = "İlkokul";
    public int? StudentCount { get; set; }
    public string? PrincipalName { get; set; }
    public string? PrincipalPhone { get; set; }
}
