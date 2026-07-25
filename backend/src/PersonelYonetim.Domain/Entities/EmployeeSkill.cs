using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

public class EmployeeSkill : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillLevel Level { get; set; }
    public string? ExperienceDuration { get; set; }
    public bool HasCertificate { get; set; }
    public DateOnly? CertificateDate { get; set; }
    public string? CertificateIssuer { get; set; }
    public string? Description { get; set; }
}
