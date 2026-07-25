using MediatR;

namespace PersonelYonetim.Application.Features.Dashboard;

/// <summary>Ana kontrol paneli özeti — Dashboard.View (şartname §5).</summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class DashboardSummaryDto
{
    public DashboardCardsDto Cards { get; init; } = new();
    public DashboardAttentionDto Attention { get; init; } = new();
    public IReadOnlyList<DashboardNamedCountDto> ByUnit { get; init; } = [];
    public IReadOnlyList<DashboardNamedCountDto> ByEmploymentType { get; init; } = [];
    public IReadOnlyList<DashboardNamedCountDto> ByDutyCategory { get; init; } = [];
    public IReadOnlyList<DashboardNamedCountDto> ByEducationLevel { get; init; } = [];
    public IReadOnlyList<DashboardNamedCountDto> ByServiceYears { get; init; } = [];
}

/// <summary>Kontrol paneli aksiyon bandı — yetkiye göre doldurulur.</summary>
public sealed class DashboardAttentionDto
{
    public int UnreadNotifications { get; init; }
    public int CertificateExpired { get; init; }
    public int CertificateExpiringSoon { get; init; }
    public int RecentMovements30Days { get; init; }
    public IReadOnlyList<DashboardAlertItemDto> Alerts { get; init; } = [];
    public IReadOnlyList<DashboardRecentMovementDto> RecentMovements { get; init; } = [];
    public IReadOnlyList<DashboardQuickLinkDto> QuickLinks { get; init; } = [];
}

public sealed class DashboardAlertItemDto
{
    public string Code { get; init; } = string.Empty;
    /// <summary>danger | warn | info</summary>
    public string Severity { get; init; } = "info";
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Count { get; init; }
    public string Href { get; init; } = string.Empty;
}

public sealed class DashboardRecentMovementDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string MovementTypeLabel { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public string? Summary { get; init; }
}

public sealed class DashboardQuickLinkDto
{
    public string Label { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class DashboardCardsDto
{
    public int TotalEmployees { get; init; }
    public int ActiveEmployees { get; init; }
    public int PassiveEmployees { get; init; }
    public int TotalUnits { get; init; }
    public int TotalFacilities { get; init; }
    public int ActiveFacilities { get; init; }
    public int ClosedOrRenovationFacilities { get; init; }
    public int CivilServantCount { get; init; }
    public int CompanyStaffCount { get; init; }
    public int InstructorCount { get; init; }
    public int TechnicalCount { get; init; }
    public int LibraryStaffCount { get; init; }
    public int AuxiliaryCount { get; init; }
    public int IncompleteProfileCount { get; init; }
    public int MissingSkillsCount { get; init; }
    public int WorkplaceChangedCount { get; init; }
}

public sealed class DashboardNamedCountDto
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}
