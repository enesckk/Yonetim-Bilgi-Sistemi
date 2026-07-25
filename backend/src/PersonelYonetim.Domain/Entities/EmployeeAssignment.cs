using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Personelin fiili görev ataması.
/// Bir personelin birden fazla görevi olabilir; biri ana görevdir.
/// </summary>
public class EmployeeAssignment : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid JobDutyId { get; set; }
    public JobDuty JobDuty { get; set; } = null!;

    public bool IsPrimary { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
}
