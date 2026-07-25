using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Personelin ana kaydı.
/// Hassas alanlar (TCKN vb.) ayrı tabloda / şifreli tutulur.
/// </summary>
public class Employee : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PhotoPath { get; set; }

    public DateOnly? BirthDate { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public string? PersonalPhone { get; set; }
    public string? CorporatePhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? CorporateEmail { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Bağlı olduğu birim (ana/alt birim).</summary>
    public Guid? UnitId { get; set; }
    public OrganizationUnit? Unit { get; set; }

    /// <summary>Fiilen çalıştığı tesis (birimden farklı olabilir).</summary>
    public Guid? FacilityId { get; set; }
    public OrganizationUnit? Facility { get; set; }

    public Guid? EmploymentTypeId { get; set; }
    public EmploymentType? EmploymentType { get; set; }

    public Guid? JobTitleId { get; set; }
    public JobTitle? JobTitle { get; set; }

    public Guid? ManagerEmployeeId { get; set; }
    public Employee? ManagerEmployee { get; set; }

    public DateOnly? HireDate { get; set; }
    public DateOnly? DirectorateStartDate { get; set; }
    public DateOnly? UnitStartDate { get; set; }
    public DateOnly? DutyStartDate { get; set; }

    /// <summary>0–100 arası bilgi tamamlama oranı (hesaplanır, saklanır).</summary>
    public byte ProfileCompletionPercent { get; set; }

    public ICollection<EmployeeAssignment> Assignments { get; set; } = new List<EmployeeAssignment>();
    public ICollection<EducationRecord> EducationRecords { get; set; } = new List<EducationRecord>();
    public ICollection<EmployeeSkill> Skills { get; set; } = new List<EmployeeSkill>();
    public ICollection<EmployeeCertificate> Certificates { get; set; } = new List<EmployeeCertificate>();
    public ICollection<EmployeeNote> Notes { get; set; } = new List<EmployeeNote>();
    public ICollection<EmployeeMovement> Movements { get; set; } = new List<EmployeeMovement>();
    public ICollection<SpecialCondition> SpecialConditions { get; set; } = new List<SpecialCondition>();
    public EmployeeSensitiveData? SensitiveData { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
