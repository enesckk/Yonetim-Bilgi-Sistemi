using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Özel durum kaydı — detay sadece yetkili rollere.
/// Liste ekranında yalnızca "kayıt var" bilgisi gösterilir.
/// </summary>
public class SpecialCondition : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string ConditionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPermanent { get; set; }
    public bool RequiresDutyAdjustment { get; set; }
    public bool RequiresWorkspaceAdjustment { get; set; }
    public bool HasDocument { get; set; }
    public string? DocumentPath { get; set; }
}
