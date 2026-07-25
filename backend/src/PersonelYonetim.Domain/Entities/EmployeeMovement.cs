using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Personel hareketi — birim/tesis/görev değişikliği geçmişi.
/// Eski kayıt silinmez; zaman çizelgesi bu tablodan okunur.
/// </summary>
public class EmployeeMovement : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public MovementType MovementType { get; set; }

    public Guid? OldUnitId { get; set; }
    public Guid? NewUnitId { get; set; }
    public Guid? OldFacilityId { get; set; }
    public Guid? NewFacilityId { get; set; }
    public Guid? OldJobTitleId { get; set; }
    public Guid? NewJobTitleId { get; set; }
    public Guid? OldJobDutyId { get; set; }
    public Guid? NewJobDutyId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public string? ApprovedBy { get; set; }
}
