using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>İstihdam türü: Memur, Şekabel, Sözleşmeli vb. (lookup).</summary>
public class EmploymentType : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
