using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

public class EmployeeCertificate : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid? CertificateDefinitionId { get; set; }
    public CertificateDefinition? CertificateDefinition { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? Category { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public string? DocumentPath { get; set; }

    public Guid? RelatedSkillId { get; set; }
    public Skill? RelatedSkill { get; set; }
}
