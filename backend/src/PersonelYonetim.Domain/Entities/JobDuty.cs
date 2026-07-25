using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Fiili görev tanımı (ör. Ses ve ışık sistemleri sorumlusu).
/// Resmi unvan ile karıştırılmaz.
/// </summary>
public class JobDuty : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public DutyCategory Category { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeAssignment> Assignments { get; set; } = new List<EmployeeAssignment>();
}
