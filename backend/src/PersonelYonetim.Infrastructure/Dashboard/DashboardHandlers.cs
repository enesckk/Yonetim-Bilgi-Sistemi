using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Dashboard;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Movements;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Reports;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Dashboard;

public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private const byte IncompleteProfileThreshold = 80;
    private const int CertificateSoonDays = 90;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardSummaryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.DashboardView))
            throw new ForbiddenException("Kontrol paneli için Dashboard.View gerekir.");

        var employees = _db.Employees.AsNoTracking().AsQueryable();
        var scoped = await UnitScopeHelper.ApplyAsync(_db, _currentUser, employees, cancellationToken);
        if (scoped is null)
            return Empty();

        employees = scoped;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employeeIds = employees.Select(x => x.Id);

        var total = await employees.CountAsync(cancellationToken);
        var active = await employees.CountAsync(x => x.Status == EmployeeStatus.Active, cancellationToken);
        var passive = await employees.CountAsync(x => x.Status == EmployeeStatus.Passive, cancellationToken);

        var allowedUnits = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        var unitsQuery = _db.OrganizationUnits.AsNoTracking().AsQueryable();
        if (allowedUnits is not null)
            unitsQuery = unitsQuery.Where(x => allowedUnits.Contains(x.Id));

        var totalUnits = await unitsQuery
            .CountAsync(x => x.Type == OrganizationUnitType.MainUnit, cancellationToken);

        var facilities = unitsQuery.Where(x => x.Type == OrganizationUnitType.Facility);
        var totalFacilities = await facilities.CountAsync(cancellationToken);
        var activeFacilities = await facilities.CountAsync(
            x => x.Status == OrganizationUnitStatus.Active, cancellationToken);
        var closedOrRenovation = await facilities.CountAsync(
            x => x.Status == OrganizationUnitStatus.Closed
                 || x.Status == OrganizationUnitStatus.UnderRenovation
                 || x.Status == OrganizationUnitStatus.TemporarilyClosed,
            cancellationToken);

        var civilServant = await employees.CountAsync(
            x => x.EmploymentType != null && x.EmploymentType.Code == "MEMUR",
            cancellationToken);
        var companyStaff = await employees.CountAsync(
            x => x.EmploymentType != null
                 && (x.EmploymentType.Code == "SEKABEL" || x.EmploymentType.Code == "DIGER_SIRKET"),
            cancellationToken);

        var dutyRows = await employees
            .Select(e => e.Assignments
                .Where(a => a.EndDate == null || a.EndDate >= today)
                .OrderByDescending(a => a.IsPrimary)
                .Select(a => (DutyCategory?)a.JobDuty.Category)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        int CountDuty(DutyCategory cat) => dutyRows.Count(x => x == cat);

        var incomplete = await employees.CountAsync(
            x => x.ProfileCompletionPercent < IncompleteProfileThreshold,
            cancellationToken);
        var missingSkills = await employees.CountAsync(x => !x.Skills.Any(), cancellationToken);

        var workplaceChanged = await _db.EmployeeMovements.AsNoTracking()
            .Where(m => employeeIds.Contains(m.EmployeeId)
                        && (m.MovementType == MovementType.UnitChange
                            || m.MovementType == MovementType.FacilityChange))
            .Select(m => m.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);

        var unitRows = await employees
            .Where(x => x.UnitId != null)
            .Select(x => new
            {
                x.Unit!.Name,
                x.Unit.Type,
                ParentName = x.Unit.Parent != null ? x.Unit.Parent.Name : null,
                ParentType = x.Unit.Parent != null ? (OrganizationUnitType?)x.Unit.Parent.Type : null,
                GrandParentName = x.Unit.Parent != null && x.Unit.Parent.Parent != null
                    ? x.Unit.Parent.Parent.Name
                    : null
            })
            .ToListAsync(cancellationToken);

        var byUnit = unitRows
            .Select(x => ResolveMainUnitName(x.Name, x.Type, x.ParentName, x.ParentType, x.GrandParentName))
            .GroupBy(x => x)
            .Select(g => new DashboardNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var byEmployment = await employees
            .GroupBy(x => x.EmploymentType != null ? x.EmploymentType.Name : "Belirtilmemiş")
            .Select(g => new DashboardNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var byDuty = DutyCategoryLabels
            .Select(kv => new DashboardNamedCountDto
            {
                Name = kv.Value,
                Count = dutyRows.Count(x => x == kv.Key)
            })
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();

        var noDuty = dutyRows.Count(x => x is null);
        if (noDuty > 0)
            byDuty.Add(new DashboardNamedCountDto { Name = "Görev atanmamış", Count = noDuty });
        byDuty = byDuty.OrderByDescending(x => x.Count).ToList();

        var educationLevels = await employees
            .Select(e => e.EducationRecords.Any()
                ? e.EducationRecords.Max(r => r.Level)
                : EducationLevel.Unknown)
            .ToListAsync(cancellationToken);

        var byEducation = EducationLabels
            .Select(kv => new DashboardNamedCountDto
            {
                Name = kv.Value,
                Count = educationLevels.Count(x => x == kv.Key)
            })
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();

        var eduUnknown = byEducation.FirstOrDefault(x => x.Name == "Bilgisi girilmemiş");
        if (eduUnknown is not null)
        {
            byEducation = byEducation.Where(x => x.Name != "Bilgisi girilmemiş").ToList();
            byEducation.Add(eduUnknown);
        }

        var hireDates = await employees.Select(x => x.HireDate).ToListAsync(cancellationToken);
        var byService = BuildServiceYearBuckets(hireDates, today)
            .Where(x => x.Count > 0)
            .ToList();

        var cards = new DashboardCardsDto
        {
            TotalEmployees = total,
            ActiveEmployees = active,
            PassiveEmployees = passive,
            TotalUnits = totalUnits,
            TotalFacilities = totalFacilities,
            ActiveFacilities = activeFacilities,
            ClosedOrRenovationFacilities = closedOrRenovation,
            CivilServantCount = civilServant,
            CompanyStaffCount = companyStaff,
            InstructorCount = CountDuty(DutyCategory.Instructor),
            TechnicalCount = CountDuty(DutyCategory.Technical),
            LibraryStaffCount = CountDuty(DutyCategory.Library),
            AuxiliaryCount = CountDuty(DutyCategory.Auxiliary),
            IncompleteProfileCount = incomplete,
            MissingSkillsCount = missingSkills,
            WorkplaceChangedCount = workplaceChanged
        };

        var attention = await BuildAttentionAsync(employeeIds, cards, today, cancellationToken);

        return new DashboardSummaryDto
        {
            Cards = cards,
            Attention = attention,
            ByUnit = byUnit,
            ByEmploymentType = byEmployment,
            ByDutyCategory = byDuty,
            ByEducationLevel = byEducation,
            ByServiceYears = byService
        };
    }

    private async Task<DashboardAttentionDto> BuildAttentionAsync(
        IQueryable<Guid> employeeIds,
        DashboardCardsDto cards,
        DateOnly today,
        CancellationToken ct)
    {
        var alerts = new List<DashboardAlertItemDto>();
        var canNotifications = _currentUser.HasPermission(PermissionCodes.NotificationsView);
        var canDataQuality = _currentUser.HasPermission(PermissionCodes.DataQualityView);
        var canEmployees = _currentUser.HasPermission(PermissionCodes.EmployeesView);
        var canMovements = _currentUser.HasPermission(PermissionCodes.MovementsView);
        var canOrg = _currentUser.HasPermission(PermissionCodes.OrganizationView);
        var canReports = _currentUser.HasPermission(PermissionCodes.ReportsView);

        var unread = 0;
        if (canNotifications && _currentUser.UserId is Guid userId)
        {
            unread = await _db.UserNotifications.AsNoTracking()
                .CountAsync(x => x.UserId == userId && !x.IsRead, ct);

            if (unread > 0)
            {
                alerts.Add(new DashboardAlertItemDto
                {
                    Code = "unread-notifications",
                    Severity = "info",
                    Title = "Okunmamış bildirim",
                    Description = "Bildirimler ekranından inceleyin.",
                    Count = unread,
                    Href = "/notifications"
                });
            }
        }

        var certExpired = 0;
        var certSoon = 0;
        if (canEmployees)
        {
            var soonLimit = today.AddDays(CertificateSoonDays);
            var certs = _db.EmployeeCertificates.AsNoTracking()
                .Where(c => employeeIds.Contains(c.EmployeeId) && c.ExpiresOn != null);

            certExpired = await certs.CountAsync(c => c.ExpiresOn < today, ct);
            certSoon = await certs.CountAsync(
                c => c.ExpiresOn >= today && c.ExpiresOn <= soonLimit, ct);

            if (certExpired > 0)
            {
                alerts.Add(new DashboardAlertItemDto
                {
                    Code = "cert-expired",
                    Severity = "danger",
                    Title = "Süresi dolmuş sertifika",
                    Description = "Yenileme veya güncelleme gerekebilir.",
                    Count = certExpired,
                    Href = "/certificates"
                });
            }

            if (certSoon > 0)
            {
                alerts.Add(new DashboardAlertItemDto
                {
                    Code = "cert-soon",
                    Severity = "warn",
                    Title = "Sertifika süresi yaklaşıyor",
                    Description = $"{CertificateSoonDays} gün içinde dolacak kayıtlar.",
                    Count = certSoon,
                    Href = "/certificates"
                });
            }
        }

        if (canDataQuality && cards.IncompleteProfileCount > 0)
        {
            alerts.Add(new DashboardAlertItemDto
            {
                Code = "incomplete-profile",
                Severity = "warn",
                Title = "Eksik bilgili personel",
                Description = $"Profil tamamlanma oranı %{IncompleteProfileThreshold} altında.",
                Count = cards.IncompleteProfileCount,
                Href = "/data-quality?kind=missing"
            });
        }

        if (canDataQuality && cards.MissingSkillsCount > 0)
        {
            alerts.Add(new DashboardAlertItemDto
            {
                Code = "missing-skills",
                Severity = "warn",
                Title = "Yetkinlik girilmemiş",
                Description = "Personel kartından yetkinlik ekleyebilirsiniz.",
                Count = cards.MissingSkillsCount,
                Href = "/data-quality"
            });
        }

        if (canOrg && cards.ClosedOrRenovationFacilities > 0)
        {
            alerts.Add(new DashboardAlertItemDto
            {
                Code = "facility-closed",
                Severity = "info",
                Title = "Kapalı / tadilatta tesis",
                Description = "Tesisler listesinden durumları kontrol edin.",
                Count = cards.ClosedOrRenovationFacilities,
                Href = "/facilities"
            });
        }

        var recentFrom = today.AddDays(-30);
        var recentCount = 0;
        IReadOnlyList<DashboardRecentMovementDto> recentItems = [];

        if (canMovements)
        {
            recentCount = await _db.EmployeeMovements.AsNoTracking()
                .CountAsync(m => employeeIds.Contains(m.EmployeeId) && m.StartDate >= recentFrom, ct);

            var rows = await _db.EmployeeMovements.AsNoTracking()
                .Where(m => employeeIds.Contains(m.EmployeeId) && m.Employee.Status == EmployeeStatus.Active)
                .OrderByDescending(m => m.StartDate)
                .ThenByDescending(m => m.CreatedAtUtc)
                .Take(6)
                .Select(m => new
                {
                    m.Id,
                    m.EmployeeId,
                    EmployeeName = m.Employee.FirstName + " " + m.Employee.LastName,
                    m.MovementType,
                    m.StartDate,
                    m.Reason,
                    OldUnit = m.OldUnitId != null
                        ? _db.OrganizationUnits.Where(u => u.Id == m.OldUnitId).Select(u => u.Name).FirstOrDefault()
                        : null,
                    NewUnit = m.NewUnitId != null
                        ? _db.OrganizationUnits.Where(u => u.Id == m.NewUnitId).Select(u => u.Name).FirstOrDefault()
                        : null,
                    OldFacility = m.OldFacilityId != null
                        ? _db.OrganizationUnits.Where(u => u.Id == m.OldFacilityId).Select(u => u.Name).FirstOrDefault()
                        : null,
                    NewFacility = m.NewFacilityId != null
                        ? _db.OrganizationUnits.Where(u => u.Id == m.NewFacilityId).Select(u => u.Name).FirstOrDefault()
                        : null
                })
                .ToListAsync(ct);

            recentItems = rows.Select(m =>
            {
                string? summary = null;
                if (m.OldUnit != null || m.NewUnit != null)
                    summary = $"{m.OldUnit ?? "—"} → {m.NewUnit ?? "—"}";
                else if (m.OldFacility != null || m.NewFacility != null)
                    summary = $"{m.OldFacility ?? "—"} → {m.NewFacility ?? "—"}";
                else if (!string.IsNullOrWhiteSpace(m.Reason))
                    summary = m.Reason;

                return new DashboardRecentMovementDto
                {
                    Id = m.Id,
                    EmployeeId = m.EmployeeId,
                    EmployeeName = m.EmployeeName.Trim(),
                    MovementTypeLabel = MovementLabels.For(m.MovementType),
                    StartDate = m.StartDate,
                    Summary = summary
                };
            }).ToList();

            if (recentCount > 0)
            {
                alerts.Add(new DashboardAlertItemDto
                {
                    Code = "recent-movements",
                    Severity = "info",
                    Title = "Son 30 günde hareket",
                    Description = "Görev geçmişinden tüm kayıtları görün.",
                    Count = recentCount,
                    Href = "/movements"
                });
            }
        }

        static int Rank(string s) => s switch { "danger" => 0, "warn" => 1, _ => 2 };
        alerts = alerts.OrderBy(a => Rank(a.Severity)).ThenByDescending(a => a.Count).ToList();

        var quick = new List<DashboardQuickLinkDto>();
        if (canEmployees)
            quick.Add(new() { Label = "Personeller", Href = "/employees", Description = "Liste ve filtreler" });
        if (canDataQuality)
            quick.Add(new() { Label = "Veri eksikleri", Href = "/data-quality", Description = "Eksik ve tutarsız kayıtlar" });
        if (canEmployees)
            quick.Add(new() { Label = "Sertifikalar", Href = "/certificates", Description = "Süre ve katalog" });
        if (canMovements)
            quick.Add(new() { Label = "Görev geçmişi", Href = "/movements", Description = "Kurum geneli hareketler" });
        if (canReports)
            quick.Add(new() { Label = "Raporlar", Href = "/reports", Description = "Hazır ve özel raporlar" });
        if (canNotifications)
            quick.Add(new() { Label = "Bildirimler", Href = "/notifications", Description = "Uyarı kutusu" });
        if (canOrg)
            quick.Add(new() { Label = "Birimler", Href = "/units", Description = "Organizasyon yapısı" });

        return new DashboardAttentionDto
        {
            UnreadNotifications = unread,
            CertificateExpired = certExpired,
            CertificateExpiringSoon = certSoon,
            RecentMovements30Days = recentCount,
            Alerts = alerts,
            RecentMovements = recentItems,
            QuickLinks = quick
        };
    }

    private static DashboardSummaryDto Empty() => new()
    {
        Cards = new DashboardCardsDto(),
        Attention = new DashboardAttentionDto(),
        ByUnit = [],
        ByEmploymentType = [],
        ByDutyCategory = [],
        ByEducationLevel = EducationLabels
            .Select(kv => new DashboardNamedCountDto { Name = kv.Value, Count = 0 })
            .ToList(),
        ByServiceYears = ServiceYearBucketNames
            .Select(n => new DashboardNamedCountDto { Name = n, Count = 0 })
            .ToList()
    };

    private static string ResolveMainUnitName(
        string name,
        OrganizationUnitType type,
        string? parentName,
        OrganizationUnitType? parentType,
        string? grandParentName)
    {
        if (type == OrganizationUnitType.MainUnit)
            return name;
        if (parentType == OrganizationUnitType.MainUnit && parentName is not null)
            return parentName;
        if (grandParentName is not null && parentType == OrganizationUnitType.SubUnit)
            return grandParentName;
        return name;
    }

    private static IReadOnlyList<DashboardNamedCountDto> BuildServiceYearBuckets(
        List<DateOnly?> hireDates,
        DateOnly today)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["0–1 yıl"] = 0,
            ["1–3 yıl"] = 0,
            ["3–5 yıl"] = 0,
            ["5–10 yıl"] = 0,
            ["10 yıl ve üzeri"] = 0,
            ["Bilgisi girilmemiş"] = 0
        };

        foreach (var hire in hireDates)
        {
            if (hire is null)
            {
                buckets["Bilgisi girilmemiş"]++;
                continue;
            }

            var years = (today.DayNumber - hire.Value.DayNumber) / 365.25;
            if (years < 1) buckets["0–1 yıl"]++;
            else if (years < 3) buckets["1–3 yıl"]++;
            else if (years < 5) buckets["3–5 yıl"]++;
            else if (years < 10) buckets["5–10 yıl"]++;
            else buckets["10 yıl ve üzeri"]++;
        }

        return ServiceYearBucketNames
            .Select(n => new DashboardNamedCountDto { Name = n, Count = buckets[n] })
            .ToList();
    }

    private static readonly Dictionary<DutyCategory, string> DutyCategoryLabels = new()
    {
        [DutyCategory.Manager] = "Yönetici",
        [DutyCategory.Administrative] = "İdari personel",
        [DutyCategory.Instructor] = "Eğitmen",
        [DutyCategory.Technical] = "Teknik personel",
        [DutyCategory.Reception] = "Danışma personeli",
        [DutyCategory.Library] = "Kütüphane personeli",
        [DutyCategory.Auxiliary] = "Yardımcı personel",
        [DutyCategory.Cleaning] = "Temizlik personeli",
        [DutyCategory.Project] = "Proje personeli",
        [DutyCategory.SocialMedia] = "Sosyal medya personeli",
        [DutyCategory.PublicRelations] = "Halkla ilişkiler personeli",
        [DutyCategory.Other] = "Diğer"
    };

    private static readonly Dictionary<EducationLevel, string> EducationLabels = new()
    {
        [EducationLevel.Primary] = "İlköğretim",
        [EducationLevel.HighSchool] = "Lise",
        [EducationLevel.AssociateDegree] = "Ön lisans",
        [EducationLevel.Bachelor] = "Lisans",
        [EducationLevel.Master] = "Yüksek lisans",
        [EducationLevel.Doctorate] = "Doktora",
        [EducationLevel.Unknown] = "Bilgisi girilmemiş"
    };

    private static readonly string[] ServiceYearBucketNames =
    [
        "0–1 yıl",
        "1–3 yıl",
        "3–5 yıl",
        "5–10 yıl",
        "10 yıl ve üzeri",
        "Bilgisi girilmemiş"
    ];
}
