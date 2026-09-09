using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Application.Features.Reports;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Settings;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PersonelYonetim.Infrastructure.Reports;

/// <summary>
/// PDF export — QuestPDF ile sabit sayfa belgesi.
/// Excel hücreleri düzenlenebilir; PDF yazdırma / resmi paylaşım içindir.
/// </summary>
public sealed class ExportEmployeesPdfHandler
    : IRequestHandler<ExportEmployeesPdfQuery, ReportFileResult>
{
    /// <summary>PDF tablo sayfa düzeni daha ağır; Excel'den düşük üst sınır.</summary>
    public const int MaxRows = 1000;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IAppSettingsService _settings;

    static ExportEmployeesPdfHandler()
    {
        // Community lisansı — ticari olmayan / uygun kullanım için
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportEmployeesPdfHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        IUserNotificationService notifications,
        IAppSettingsService settings)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _notifications = notifications;
        _settings = settings;
    }

    public async Task<ReportFileResult> Handle(
        ExportEmployeesPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsExportPdf))
            throw new ForbiddenException("PDF dışa aktarım için Reports.ExportPdf gerekir.");

        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Personel verisi için Employees.View gerekir.");

        var canPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);

        var maxRows = await _settings.GetIntAsync(
            AppSettingKeys.ReportsPdfMaxRows,
            MaxRows,
            cancellationToken);
        var orgName = await _settings.GetStringAsync(
            AppSettingKeys.OrganizationDisplayName,
            "Şehitkamil Kültür Müdürlüğü",
            cancellationToken);

        var rows = await EmployeeExportQuery.LoadAsync(
            _db,
            _currentUser,
            request,
            maxRows,
            cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "Export",
            EntityName = "Employee",
            EntityId = null,
            UserId = _currentUser.UserId?.ToString(),
            UserName = _currentUser.UserName ?? "system",
            NewValuesJson = JsonSerializer.Serialize(new
            {
                format = "pdf",
                rowCount = rows.Count,
                search = request.Search,
                status = request.Status?.ToString(),
                unitId = request.UnitId,
                facilityId = request.FacilityId,
                employmentTypeId = request.EmploymentTypeId,
                includeSubUnits = request.IncludeSubUnits
            })
        });
        await _db.SaveChangesAsync(cancellationToken);

        if (_currentUser.UserId is Guid uid)
        {
            await _notifications.NotifyAsync(
                uid,
                "PDF dışa aktarım tamamlandı",
                $"{rows.Count} personel satırı PDF belgesine yazıldı.",
                NotificationSeverity.Info,
                "Export",
                "/reports",
                cancellationToken);
        }

        var bytes = BuildPdf(
            rows,
            canPhone,
            canNationalId,
            _nationalId,
            _currentUser.UserName ?? "system",
            orgName,
            request);

        return new ReportFileResult
        {
            Content = bytes,
            FileName = $"personel-rapor-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
            ContentType = "application/pdf"
        };
    }

    private static byte[] BuildPdf(
        IReadOnlyList<Employee> rows,
        bool canPhone,
        bool canNationalId,
        INationalIdProtector nationalId,
        string exportedBy,
        string organizationName,
        ExportEmployeesPdfQuery request)
    {
        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Search))
            filterParts.Add($"Arama: {request.Search.Trim()}");
        if (request.Status.HasValue)
            filterParts.Add($"Durum: {EmployeeExportQuery.StatusLabel(request.Status.Value)}");
        if (request.UnitId.HasValue)
            filterParts.Add(request.IncludeSubUnits ? "Birim (+ alt birimler)" : "Birim");
        if (request.FacilityId.HasValue)
            filterParts.Add("Tesis filtresi");
        if (request.EmploymentTypeId.HasValue)
            filterParts.Add("İstihdam filtresi");
        if (request.JobTitleId.HasValue)
            filterParts.Add("Unvan filtresi");
        if (request.JobDutyId.HasValue)
            filterParts.Add("Görev filtresi");
        if (request.DutyCategory.HasValue)
            filterParts.Add("Görev kategorisi");
        if (request.EducationLevel.HasValue)
            filterParts.Add("Eğitim seviyesi");
        if (request.SkillId.HasValue)
            filterParts.Add("Yetkinlik filtresi");
        if (request.IncompleteProfileOnly)
            filterParts.Add("Eksik profil");
        if (request.MissingSkillsOnly)
            filterParts.Add("Yetkinlik eksik");
        if (request.MissingPhoneOnly)
            filterParts.Add("Telefon eksik");
        if (request.MissingFacilityOnly)
            filterParts.Add("Tesis eksik");
        if (request.HasSpecialConditionOnly)
            filterParts.Add("Özel durumlu");
        if (request.HireYearFrom is > 0 || request.HireYearTo is > 0)
            filterParts.Add($"İşe giriş: {request.HireYearFrom?.ToString() ?? "…"}–{request.HireYearTo?.ToString() ?? "…"}");
        var filterText = filterParts.Count == 0 ? "Filtre yok (kapsam içi tümü)" : string.Join(" · ", filterParts);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"{organizationName} — Personel Listesi")
                        .SemiBold().FontSize(14);
                    col.Item().Text($"Oluşturan: {exportedBy} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(filterText).FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    var cols = new List<float> { 2.2f, 1.2f, 1.2f, 1.8f, 1.8f, 1.4f, 0.8f };
                    if (canPhone) cols.Add(1.4f);
                    if (canNationalId) cols.Add(1.3f);

                    table.ColumnsDefinition(def =>
                    {
                        foreach (var w in cols)
                            def.RelativeColumn(w);
                    });

                    table.Header(header =>
                    {
                        void H(string t) => header.Cell().Element(CellHeader).Text(t).SemiBold();
                        H("Ad Soyad");
                        H("Sicil");
                        H("Durum");
                        H("Unvan");
                        H("Birim");
                        H("İstihdam");
                        H("Skor");
                        if (canPhone) H("Telefon");
                        if (canNationalId) H("TCKN");
                    });

                    foreach (var e in rows)
                    {
                        void C(string? t) =>
                            table.Cell().Element(CellBody).Text(t ?? "—");

                        C(e.FullName);
                        C(e.EmployeeNumber);
                        C(EmployeeExportQuery.StatusLabel(e.Status));
                        C(e.JobTitle?.Name);
                        C(e.Unit?.Name);
                        C(e.EmploymentType?.Name);
                        C($"%{e.ProfileCompletionPercent}");
                        if (canPhone)
                            C(e.CorporatePhone ?? e.PersonalPhone);
                        if (canNationalId)
                            C(nationalId.Unprotect(e.SensitiveData?.NationalIdEncrypted));
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                    t.Span($" · {rows.Count} kayıt").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return doc.GeneratePdf();

        static IContainer CellHeader(IContainer c) =>
            c.BorderBottom(1).BorderColor(Colors.Grey.Medium)
                .PaddingVertical(4).PaddingHorizontal(3)
                .Background(Colors.Grey.Lighten3);

        static IContainer CellBody(IContainer c) =>
            c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(3).PaddingHorizontal(3);
    }
}

/// <summary>Excel ve PDF export için ortak sorgu / filtre / limit.</summary>
internal static class EmployeeExportQuery
{
    private const byte IncompleteProfileThreshold = 80;

    public static async Task<List<Employee>> LoadAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        EmployeeListQuery filter,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var query = db.Employees
            .AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Facility)
            .Include(x => x.EmploymentType)
            .Include(x => x.JobTitle)
            .Include(x => x.SpecialConditions)
            .Include(x => x.SensitiveData)
            .AsQueryable();

        var filtered = await ApplyFiltersAsync(db, currentUser, query, filter, cancellationToken);
        if (filtered is null)
            return [];

        query = filtered;

        var total = await query.CountAsync(cancellationToken);
        if (total > maxRows)
            throw new ValidationException(
                "export",
                $"Sonuç {total} satır; üst sınır {maxRows}. Filtreleri daraltın.");

        return await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Birim kapsamı + tüm liste filtreleri — rapor oluşturucu da bu yolu kullanır.
    /// null dönerse kullanıcının veri kapsamı yok demektir.
    /// </summary>
    public static async Task<IQueryable<Employee>?> ApplyFiltersAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        IQueryable<Employee> query,
        EmployeeListQuery filter,
        CancellationToken cancellationToken)
    {
        var scoped = await UnitScopeHelper.ApplyAsync(db, currentUser, query, cancellationToken);
        if (scoped is null)
            return null;

        query = scoped;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(term)
                || x.LastName.ToLower().Contains(term)
                || (x.FirstName + " " + x.LastName).ToLower().Contains(term)
                || (x.EmployeeNumber != null && x.EmployeeNumber.ToLower().Contains(term))
                || (x.PersonalPhone != null && x.PersonalPhone.Contains(term))
                || (x.CorporatePhone != null && x.CorporatePhone.Contains(term))
                || (x.PersonalEmail != null && x.PersonalEmail.ToLower().Contains(term))
                || (x.CorporateEmail != null && x.CorporateEmail.ToLower().Contains(term))
                || (x.JobTitle != null && x.JobTitle.Name.ToLower().Contains(term))
                || (x.Unit != null && x.Unit.Name.ToLower().Contains(term))
                || (x.Facility != null && x.Facility.Name.ToLower().Contains(term))
                || (x.EmploymentType != null && x.EmploymentType.Name.ToLower().Contains(term))
                || x.Assignments.Any(a => a.JobDuty != null && a.JobDuty.Name.ToLower().Contains(term))
                || x.EducationRecords.Any(e =>
                    (e.Department != null && e.Department.ToLower().Contains(term))
                    || (e.University != null && e.University.ToLower().Contains(term))
                    || (e.School != null && e.School.ToLower().Contains(term)))
                || x.Skills.Any(s => s.Skill != null && s.Skill.Name.ToLower().Contains(term)));
        }

        if (filter.UnitId.HasValue)
        {
            if (filter.IncludeSubUnits)
            {
                var unitIds = await GetUnitAndDescendantIdsAsync(db, filter.UnitId.Value, cancellationToken);
                query = query.Where(x => x.UnitId != null && unitIds.Contains(x.UnitId.Value));
            }
            else
            {
                query = query.Where(x => x.UnitId == filter.UnitId);
            }
        }

        if (filter.FacilityId.HasValue)
            query = query.Where(x => x.FacilityId == filter.FacilityId);

        if (filter.EmploymentTypeId.HasValue)
            query = query.Where(x => x.EmploymentTypeId == filter.EmploymentTypeId);

        if (filter.JobTitleId.HasValue)
            query = query.Where(x => x.JobTitleId == filter.JobTitleId);

        if (filter.JobDutyId.HasValue)
            query = query.Where(x =>
                x.Assignments.Any(a => a.JobDutyId == filter.JobDutyId && a.EndDate == null));

        if (filter.DutyCategory.HasValue)
            query = query.Where(x =>
                x.Assignments.Any(a =>
                    a.EndDate == null &&
                    a.JobDuty != null &&
                    a.JobDuty.Category == filter.DutyCategory));

        if (filter.EducationLevel.HasValue)
            query = query.Where(x =>
                x.EducationRecords.Any(e => e.Level == filter.EducationLevel));

        if (filter.SkillId.HasValue)
            query = query.Where(x => x.Skills.Any(s => s.SkillId == filter.SkillId));

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status);

        if (filter.IncompleteProfileOnly)
            query = query.Where(x => x.ProfileCompletionPercent < IncompleteProfileThreshold);

        if (filter.MissingSkillsOnly)
            query = query.Where(x => !x.Skills.Any());

        if (filter.MissingPhoneOnly)
            query = query.Where(x =>
                (x.PersonalPhone == null || x.PersonalPhone == string.Empty) &&
                (x.CorporatePhone == null || x.CorporatePhone == string.Empty));

        if (filter.MissingFacilityOnly)
            query = query.Where(x => x.FacilityId == null || x.Facility == null);

        if (filter.HasSpecialConditionOnly)
            query = query.Where(x => x.SpecialConditions.Any());

        if (filter.HireYearFrom is int yearFrom and > 0)
            query = query.Where(x => x.HireDate != null && x.HireDate.Value.Year >= yearFrom);

        if (filter.HireYearTo is int yearTo and > 0)
            query = query.Where(x => x.HireDate != null && x.HireDate.Value.Year <= yearTo);

        if (!string.IsNullOrWhiteSpace(filter.UniversityContains))
        {
            var uni = filter.UniversityContains.Trim().ToLower();
            query = query.Where(x =>
                x.EducationRecords.Any(e => e.University != null && e.University.ToLower().Contains(uni)));
        }

        if (filter.GraduationYearFrom is int gradFrom and > 0)
            query = query.Where(x =>
                x.EducationRecords.Any(e => e.GraduationYear != null && e.GraduationYear >= gradFrom));

        if (filter.GraduationYearTo is int gradTo and > 0)
            query = query.Where(x =>
                x.EducationRecords.Any(e => e.GraduationYear != null && e.GraduationYear <= gradTo));

        if (filter.HasNotesOnly)
            query = query.Where(x => x.Notes.Any());

        if (filter.MissingCertificatesOnly)
            query = query.Where(x => !x.Certificates.Any());

        if (filter.ExpiredCertificateOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(x =>
                x.Certificates.Any(c => c.ExpiresOn != null && c.ExpiresOn < today));
        }

        return query;
    }

    private static async Task<HashSet<Guid>> GetUnitAndDescendantIdsAsync(
        AppDbContext db,
        Guid rootUnitId,
        CancellationToken cancellationToken)
    {
        var rows = await db.OrganizationUnits
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = rows
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<Guid> { rootUnitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var childId in children)
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }

    public static string StatusLabel(EmployeeStatus s) => s switch
    {
        EmployeeStatus.Active => "Aktif",
        EmployeeStatus.Passive => "Pasif",
        EmployeeStatus.OnLeave => "İzinli",
        EmployeeStatus.LongTermLeave => "Uzun izin",
        EmployeeStatus.TemporaryAssignment => "Geçici görev",
        EmployeeStatus.LeftJob => "İşten ayrıldı",
        EmployeeStatus.Retired => "Emekli",
        EmployeeStatus.TransferredToOtherDirectorate => "Başka müdürlük",
        EmployeeStatus.Suspended => "Açıkta",
        EmployeeStatus.AwaitingReturn => "Dönüş bekliyor",
        _ => s.ToString()
    };
}
