using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Resmi unvan (ör. Büro Personeli, Teknik Personel).</summary>
public class JobTitle : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
