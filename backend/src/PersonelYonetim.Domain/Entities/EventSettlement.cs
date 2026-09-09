using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Etkinliğin kapsadığı yerleşim ve o yerleşimdeki katılım.
/// UniqueBeneficiaryCount yoksa haritada "erişim oranı" denmez.
/// </summary>
public class EventSettlement : AuditableEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;

    public int AttendanceCount { get; set; }
    public int? UniqueBeneficiaryCount { get; set; }
    public string? Notes { get; set; }
}
