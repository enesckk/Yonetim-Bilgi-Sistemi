using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Hiyerarşik organizasyon düğümü (birim / tesis).
/// Self-referencing: ParentId ile ağaç oluşur.
/// </summary>
public class OrganizationUnit : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public OrganizationUnitType Type { get; set; }
    public OrganizationUnitStatus Status { get; set; } = OrganizationUnitStatus.Active;

    public Guid? ParentId { get; set; }
    public OrganizationUnit? Parent { get; set; }
    public ICollection<OrganizationUnit> Children { get; set; } = new List<OrganizationUnit>();

    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    /// <summary>Harita pin'i (özellikle tesisler için).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? Capacity { get; set; }
    public string? WorkingHours { get; set; }

    public Guid? FacilityCategoryId { get; set; }
    public FacilityCategory? FacilityCategory { get; set; }

    /// <summary>Tesisler için ideal personel sayısı (kapasite analizi).</summary>
    public int? IdealStaffCount { get; set; }

    public DateOnly? OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }

    public Guid? ManagerEmployeeId { get; set; }
    public Employee? ManagerEmployee { get; set; }

    public ICollection<Employee> EmployeesInUnit { get; set; } = new List<Employee>();
    public ICollection<Employee> EmployeesAtFacility { get; set; } = new List<Employee>();
}
