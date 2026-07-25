using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public interface IEmployeeQueryService
{
    Task<PagedResult<EmployeeListItemDto>> GetListAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default);

    Task<EmployeeDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Liste filtresi — query string'den bağlanır.
/// Export sorguları da aynı alanları paylaşır.
/// </summary>
public class EmployeeListQuery
{
    public string? Search { get; set; }
    public Guid? UnitId { get; set; }
    /// <summary>UnitId seçiliyse alt birimleri de dahil et.</summary>
    public bool IncludeSubUnits { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? JobTitleId { get; set; }
    public Guid? JobDutyId { get; set; }
    public DutyCategory? DutyCategory { get; set; }
    public EducationLevel? EducationLevel { get; set; }
    public Guid? SkillId { get; set; }
    public EmployeeStatus? Status { get; set; }
    public bool IncompleteProfileOnly { get; set; }
    public bool MissingSkillsOnly { get; set; }
    public bool MissingPhoneOnly { get; set; }
    public bool MissingFacilityOnly { get; set; }
    public bool HasSpecialConditionOnly { get; set; }
    public int? HireYearFrom { get; set; }
    public int? HireYearTo { get; set; }
    /// <summary>Üniversite adı (Contains).</summary>
    public string? UniversityContains { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    public bool HasNotesOnly { get; set; }
    public bool MissingCertificatesOnly { get; set; }
    public bool ExpiredCertificateOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "lastName";
    public bool SortDesc { get; set; }
}

public sealed class EmployeeListItemDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    /// <summary>Ham dosya yolu dönülmez; görüntüleme GET /api/employees/{id}/photo ile.</summary>
    public bool HasPhoto { get; init; }

    public string? JobTitleName { get; init; }
    public string? PrimaryDutyName { get; init; }
    public string? UnitName { get; init; }
    public string? FacilityName { get; init; }
    public string? EmploymentTypeName { get; init; }

    public EmployeeStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;

    public DateOnly? HireDate { get; init; }
    public byte ProfileCompletionPercent { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }

    /// <summary>Yetki yoksa null döner (API'de maskelenir).</summary>
    public string? PersonalPhone { get; init; }
    public string? CorporatePhone { get; init; }

    public bool HasSpecialCondition { get; init; }
}
