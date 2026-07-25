using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Tesis kategorisi: kültür merkezi, spor tesisi, kütüphane vb.</summary>
public class FacilityCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<OrganizationUnit> Facilities { get; set; } = new List<OrganizationUnit>();
}
