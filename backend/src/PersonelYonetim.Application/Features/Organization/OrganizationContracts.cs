using MediatR;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Application.Features.Employees;

namespace PersonelYonetim.Application.Features.Organization;

public sealed record GetOrganizationTreeQuery : IRequest<IReadOnlyList<OrganizationUnitNodeDto>>;

public sealed record GetOrganizationUnitDetailQuery(Guid Id) : IRequest<OrganizationUnitDetailDto?>;

public sealed record GetOrganizationFormOptionsQuery : IRequest<OrganizationFormOptionsDto>;

public sealed class OrganizationUnitNodeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public OrganizationUnitType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public OrganizationUnitStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public string? ParentName { get; init; }
    public string? Description { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public Guid? FacilityCategoryId { get; init; }
    public string? FacilityCategoryName { get; init; }
    public string? Address { get; init; }
    public int? Capacity { get; init; }
    public string? WorkingHours { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string? ManagerName { get; init; }
    /// <summary>Birim sorumlusunun aktif ana (fiili) görev adı.</summary>
    public string? ManagerDutyName { get; init; }
    public int? IdealStaffCount { get; init; }
    public int ActiveEmployeeCount { get; init; }
    public int MissingStaffCount { get; init; }
    public DateOnly? OpenedOn { get; init; }
    public DateOnly? ClosedOn { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public IReadOnlyList<NamedCountDto> DutyBreakdown { get; init; } = [];
    public IReadOnlyList<OrganizationChartPersonDto> ChartPersonnel { get; init; } = [];
    public IReadOnlyList<OrganizationUnitNodeDto> Children { get; init; } = [];
}

public sealed class OrganizationChartPersonDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? JobTitleName { get; init; }
    public string? DutyName { get; init; }
    public string? EmploymentTypeName { get; init; }
}

public sealed class NamedCountDto
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class OrganizationUnitDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public OrganizationUnitType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public OrganizationUnitStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public string? ParentName { get; init; }
    public string? Description { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public Guid? FacilityCategoryId { get; init; }
    public string? FacilityCategoryName { get; init; }
    public string? Address { get; init; }
    public int? Capacity { get; init; }
    public string? WorkingHours { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string? ManagerName { get; init; }
    public int? IdealStaffCount { get; init; }
    public int ActiveEmployeeCount { get; init; }
    public int MissingStaffCount { get; init; }
    public DateOnly? OpenedOn { get; init; }
    public DateOnly? ClosedOn { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public IReadOnlyList<NamedCountDto> DutyDistribution { get; init; } = [];
    public IReadOnlyList<NamedCountDto> EmploymentTypeDistribution { get; init; } = [];
    public IReadOnlyList<NamedCountDto> Skills { get; init; } = [];
    public IReadOnlyList<OrganizationChildSummaryDto> ChildFacilities { get; init; } = [];
    public IReadOnlyList<OrganizationPersonnelGroupDto> PersonnelByDuty { get; init; } = [];
    public IReadOnlyList<OrganizationMovementSummaryDto> RecentMovements { get; init; } = [];
}

public sealed class OrganizationChildSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public int ActiveEmployeeCount { get; init; }
    public int? IdealStaffCount { get; init; }
}

public sealed class OrganizationPersonnelGroupDto
{
    public string DutyName { get; init; } = string.Empty;
    public IReadOnlyList<OrganizationPersonnelItemDto> Employees { get; init; } = [];
}

public sealed class OrganizationPersonnelItemDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    public string? JobTitleName { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public bool IsPrimaryDuty { get; init; }
}

public sealed class OrganizationMovementSummaryDto
{
    public Guid Id { get; init; }
    public string MovementTypeLabel { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public string? Reason { get; init; }
}

public sealed class OrganizationFormOptionsDto
{
    public IReadOnlyList<EnumOptionDto> Types { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> Statuses { get; init; } = [];
    public IReadOnlyList<LookupOptionDto> ParentCandidates { get; init; } = [];
    public IReadOnlyList<LookupOptionDto> Managers { get; init; } = [];
    public IReadOnlyList<LookupOptionDto> FacilityCategories { get; init; } = [];
}

public sealed class LookupOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public OrganizationUnitType? Type { get; init; }
}

public sealed class CreateOrganizationUnitCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public OrganizationUnitType Type { get; set; }
    public OrganizationUnitStatus Status { get; set; } = OrganizationUnitStatus.Active;
    public Guid? ParentId { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? FacilityCategoryId { get; set; }
    public string? Address { get; set; }
    public int? Capacity { get; set; }
    public string? WorkingHours { get; set; }
    public int? IdealStaffCount { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
}

public sealed class UpdateOrganizationUnitCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public OrganizationUnitType Type { get; set; }
    public OrganizationUnitStatus Status { get; set; } = OrganizationUnitStatus.Active;
    public Guid? ParentId { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? FacilityCategoryId { get; set; }
    public string? Address { get; set; }
    public int? Capacity { get; set; }
    public string? WorkingHours { get; set; }
    public int? IdealStaffCount { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
}

public sealed record DeleteOrganizationUnitCommand(Guid Id) : IRequest;
