using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

public class CertificateDefinition : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeCertificate> EmployeeCertificates { get; set; } = new List<EmployeeCertificate>();
}
