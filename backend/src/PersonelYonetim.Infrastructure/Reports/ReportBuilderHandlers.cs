using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Reports;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Settings;
using PersonelYonetim.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Reports;

/// <summary>
/// Rapor oluşturucu — kullanıcı seçimlerine göre otomatik tablo üretir.
/// Aynı çekirdek (ReportBuildCore) JSON, Excel ve PDF çıktısını besler.
/// </summary>
internal sealed record ReportColumnDef(
    string Key,
    string Label,
    string Group,
    string? RequiredPermission,
    Func<Employee, ReportValueContext, object?> Selector);

internal sealed class ReportValueContext
{
    public required INationalIdProtector NationalId { get; init; }
    public DateOnly Today { get; init; } = DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>Sıralaması görünen metinden farklı olan değerler (ör. hizmet aralığı).</summary>
internal sealed record SortableValue(IComparable SortKey, string Display);

internal static class ReportColumns
{
    public static readonly IReadOnlyList<ReportColumnDef> All =
    [
        new("fullName", "Ad Soyad", "Kimlik", null, (e, _) => e.FullName),
        new("employeeNumber", "Sicil No", "Kimlik", null, (e, _) => e.EmployeeNumber),
        new("status", "Durum", "Kimlik", null, (e, _) => EmployeeExportQuery.StatusLabel(e.Status)),
        new("gender", "Cinsiyet", "Kimlik", null, (e, _) => GenderLabel(e.Gender)),
        new("birthDate", "Doğum tarihi", "Kimlik", null, (e, _) => e.BirthDate),
        new("jobTitle", "Unvan", "Görev", null, (e, _) => e.JobTitle?.Name),
        new("duty", "Fiili görev", "Görev", null, (e, _) => PrimaryDuty(e)),
        new("unit", "Birim", "Görev", null, (e, _) => e.Unit?.Name),
        new("facility", "Tesis", "Görev", null, (e, _) => e.Facility?.Name),
        new("employmentType", "İstihdam türü", "Görev", null, (e, _) => e.EmploymentType?.Name),
        new("hireDate", "İşe giriş tarihi", "Görev", null, (e, _) => e.HireDate),
        new("hireYear", "İşe giriş yılı", "Görev", null, (e, _) => e.HireDate?.Year),
        new("serviceYears", "Hizmet süresi (yıl)", "Görev", null, (e, ctx) => ServiceYears(e, ctx.Today)),
        new("serviceBand", "Hizmet süresi aralığı", "Görev", null, (e, ctx) => ServiceBand(e, ctx.Today)),
        new("lastMovement", "Son hareket", "Görev", null, (e, _) => LastMovement(e)),
        new("movementCount", "Hareket sayısı", "Görev", null, (e, _) => e.Movements.Count),
        new("personalPhone", "Telefon", "İletişim", PermissionCodes.EmployeesViewPhone, (e, _) => e.PersonalPhone),
        new("corporatePhone", "Kurumsal telefon", "İletişim", PermissionCodes.EmployeesViewPhone, (e, _) => e.CorporatePhone),
        new("email", "E-posta", "İletişim", null, (e, _) => e.CorporateEmail ?? e.PersonalEmail),
        new("address", "Adres", "İletişim", PermissionCodes.EmployeesViewAddress, (e, _) => e.Address),
        new("nationalId", "TCKN", "İletişim", PermissionCodes.EmployeesViewNationalId,
            (e, ctx) => ctx.NationalId.Unprotect(e.SensitiveData?.NationalIdEncrypted)),
        new("educationLevel", "Eğitim seviyesi", "Eğitim", null, (e, _) => EducationLevelLabel(TopEducation(e)?.Level)),
        new("university", "Üniversite", "Eğitim", null, (e, _) => TopEducation(e)?.University),
        new("department", "Mezun olduğu bölüm", "Eğitim", null, (e, _) => TopEducation(e)?.Department),
        new("graduationYear", "Mezuniyet yılı", "Eğitim", null, (e, _) => TopEducation(e)?.GraduationYear),
        new("skills", "Yetkinlikler", "Yetkinlik & sertifika", null,
            (e, _) => JoinNames(e.Skills.Select(s => s.Skill?.Name))),
        new("certificates", "Sertifikalar", "Yetkinlik & sertifika", null,
            (e, _) => JoinNames(e.Certificates.Select(c => c.Name))),
        new("certificateCount", "Sertifika sayısı", "Yetkinlik & sertifika", null, (e, _) => e.Certificates.Count),
        new("profileCompletion", "Profil tamamlanma (%)", "Diğer", null, (e, _) => (int)e.ProfileCompletionPercent),
        new("specialCondition", "Özel durum", "Diğer", null, (e, _) => e.SpecialConditions.Count > 0),
    ];

    public static readonly IReadOnlyList<string> DefaultKeys =
        ["fullName", "employeeNumber", "status", "jobTitle", "unit"];

    public static IReadOnlyList<ReportColumnDef> Permitted(ICurrentUserService user) =>
        All.Where(c => c.RequiredPermission is null || user.HasPermission(c.RequiredPermission)).ToList();

    public static ReportColumnDto ToDto(ReportColumnDef def) =>
        new() { Key = def.Key, Label = def.Label, Group = def.Group };

    private static string? PrimaryDuty(Employee e) =>
        e.Assignments
            .Where(a => a.EndDate == null)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.StartDate)
            .Select(a => a.JobDuty?.Name)
            .FirstOrDefault(n => n != null);

    private static double? ServiceYears(Employee e, DateOnly today)
    {
        if (e.HireDate is not DateOnly hire || hire > today)
            return null;
        return Math.Round((today.DayNumber - hire.DayNumber) / 365.25, 1);
    }

    private static SortableValue? ServiceBand(Employee e, DateOnly today)
    {
        var years = ServiceYears(e, today);
        if (years is null)
            return null;
        return years switch
        {
            < 5 => new SortableValue(0, "5 yıldan az"),
            < 10 => new SortableValue(1, "5–10 yıl"),
            < 15 => new SortableValue(2, "10–15 yıl"),
            < 20 => new SortableValue(3, "15–20 yıl"),
            _ => new SortableValue(4, "20 yıl ve üzeri"),
        };
    }

    private static SortableValue? LastMovement(Employee e)
    {
        var m = e.Movements.OrderByDescending(x => x.StartDate).FirstOrDefault();
        if (m is null)
            return null;
        return new SortableValue(
            m.StartDate.DayNumber,
            $"{MovementLabel(m.MovementType)} · {m.StartDate:dd.MM.yyyy}");
    }

    private static EducationRecord? TopEducation(Employee e) =>
        e.EducationRecords
            .Where(r => r.Level != EducationLevel.Unknown)
            .OrderByDescending(r => (byte)r.Level)
            .ThenByDescending(r => r.GraduationYear ?? 0)
            .FirstOrDefault()
        ?? e.EducationRecords.FirstOrDefault();

    private static string? JoinNames(IEnumerable<string?> names)
    {
        var list = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct()
            .ToList();
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    public static string GenderLabel(Gender g) => g switch
    {
        Gender.Female => "Kadın",
        Gender.Male => "Erkek",
        Gender.Other => "Diğer",
        _ => "Belirtilmemiş"
    };

    public static string? EducationLevelLabel(EducationLevel? level) => level switch
    {
        null => null,
        EducationLevel.Primary => "İlköğretim",
        EducationLevel.HighSchool => "Lise",
        EducationLevel.AssociateDegree => "Ön lisans",
        EducationLevel.Bachelor => "Lisans",
        EducationLevel.Master => "Yüksek lisans",
        EducationLevel.Doctorate => "Doktora",
        _ => "Bilgisi girilmemiş"
    };

    public static string MovementLabel(MovementType t) => t switch
    {
        MovementType.UnitChange => "Birim değişikliği",
        MovementType.FacilityChange => "Tesis değişikliği",
        MovementType.DutyChange => "Görev değişikliği",
        MovementType.TitleChange => "Unvan değişikliği",
        MovementType.TemporaryAssignment => "Geçici görevlendirme",
        MovementType.PermanentAssignment => "Kalıcı görevlendirme",
        MovementType.AdditionalDutyAssigned => "Ek görev",
        MovementType.DutyRemoved => "Görev kaldırıldı",
        MovementType.TransferToOtherDirectorate => "Başka müdürlüğe geçiş",
        MovementType.LeftJob => "İşten ayrılış",
        MovementType.Retirement => "Emeklilik",
        MovementType.ReturnToDuty => "Göreve dönüş",
        _ => t.ToString()
    };
}

/// <summary>Üç çıktı biçiminin ortak sonucu.</summary>
internal sealed record BuiltReport(
    string Title,
    IReadOnlyList<ReportColumnDef> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    IReadOnlyList<string> AppliedFilters,
    string? GroupByKey,
    string? GroupByLabel,
    int TotalCount);

internal static class ReportBuildCore
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task<BuiltReport> BuildAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        ReportBuildRequest request,
        int maxRows,
        CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Personel verisi için Employees.View gerekir.");

        var permitted = ReportColumns.Permitted(currentUser);
        var byKey = permitted.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        var columns = request.Columns
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(byKey.ContainsKey)
            .Select(k => byKey[k])
            .ToList();
        if (columns.Count == 0)
            columns = ReportColumns.DefaultKeys.Select(k => byKey[k]).ToList();

        ReportColumnDef? groupCol = null;
        if (!string.IsNullOrWhiteSpace(request.GroupBy) && byKey.TryGetValue(request.GroupBy.Trim(), out var g))
        {
            groupCol = g;
            if (!columns.Contains(g))
                columns.Insert(0, g);
        }

        ReportColumnDef? orderCol = null;
        if (!string.IsNullOrWhiteSpace(request.OrderByColumn)
            && byKey.TryGetValue(request.OrderByColumn.Trim(), out var o)
            && columns.Contains(o))
        {
            orderCol = o;
        }

        var query = db.Employees
            .AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Facility)
            .Include(x => x.EmploymentType)
            .Include(x => x.JobTitle)
            .Include(x => x.SpecialConditions)
            .Include(x => x.SensitiveData)
            .Include(x => x.EducationRecords)
            .Include(x => x.Skills).ThenInclude(s => s.Skill)
            .Include(x => x.Certificates)
            .Include(x => x.Assignments).ThenInclude(a => a.JobDuty)
            .Include(x => x.Movements)
            .AsQueryable();

        var filtered = await EmployeeExportQuery.ApplyFiltersAsync(db, currentUser, query, request, ct);
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Personel raporu" : request.Title.Trim();
        var appliedFilters = await DescribeFiltersAsync(db, request, ct);

        if (filtered is null)
        {
            return new BuiltReport(title, columns, [], appliedFilters, groupCol?.Key, groupCol?.Label, 0);
        }

        query = filtered;

        if (request.HasCertificatesOnly)
            query = query.Where(x => x.Certificates.Any());

        if (request.HasMovementsOnly || request.MovementFrom.HasValue || request.MovementTo.HasValue)
        {
            var from = request.MovementFrom;
            var to = request.MovementTo;
            query = query.Where(x => x.Movements.Any(m =>
                (from == null || m.StartDate >= from)
                && (to == null || m.StartDate <= to)));
        }

        var total = await query.CountAsync(ct);
        if (total > maxRows)
            throw new AppValidationException(
                "report",
                $"Sonuç {total} satır; üst sınır {maxRows}. Filtreleri daraltın.");

        var employees = await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Take(maxRows)
            .ToListAsync(ct);

        var ctx = new ReportValueContext { NationalId = nationalId };
        var raw = employees
            .Select(e => columns.Select(c => c.Selector(e, ctx)).ToArray())
            .ToList();

        // Sıralama: önce grup sütunu, sonra seçilen sıralama sütunu (LINQ OrderBy kararlıdır;
        // DB'den gelen soyad/ad sırası eşitliklerde korunur).
        IEnumerable<object?[]> ordered = raw;
        var groupIdx = groupCol is null ? -1 : columns.IndexOf(groupCol);
        var orderIdx = orderCol is null ? -1 : columns.IndexOf(orderCol);
        if (groupIdx >= 0 && orderIdx >= 0)
        {
            var o1 = raw.OrderBy(r => r[groupIdx], RawComparer.Instance);
            ordered = request.OrderByDesc
                ? o1.ThenByDescending(r => r[orderIdx], RawComparer.Instance)
                : o1.ThenBy(r => r[orderIdx], RawComparer.Instance);
        }
        else if (groupIdx >= 0)
        {
            ordered = raw.OrderBy(r => r[groupIdx], RawComparer.Instance);
        }
        else if (orderIdx >= 0)
        {
            ordered = request.OrderByDesc
                ? raw.OrderByDescending(r => r[orderIdx], RawComparer.Instance)
                : raw.OrderBy(r => r[orderIdx], RawComparer.Instance);
        }

        var rows = ordered
            .Select(r => (IReadOnlyList<string?>)r.Select(Format).ToList())
            .ToList();

        return new BuiltReport(title, columns, rows, appliedFilters, groupCol?.Key, groupCol?.Label, rows.Count);
    }

    public static string? Format(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b ? "Evet" : "Hayır",
        DateOnly d => d.ToString("dd.MM.yyyy"),
        DateTime dt => dt.ToString("dd.MM.yyyy HH:mm"),
        double dbl => dbl.ToString("0.#", Tr),
        SortableValue sv => sv.Display,
        IFormattable f => f.ToString(null, Tr),
        _ => value.ToString()
    };

    private sealed class RawComparer : IComparer<object?>
    {
        public static readonly RawComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is SortableValue sx) x = sx.SortKey;
            if (y is SortableValue sy) y = sy.SortKey;
            if (x is null && y is null) return 0;
            if (x is null) return 1;  // boşlar sona
            if (y is null) return -1;
            if (x is string a && y is string b)
                return string.Compare(a, b, Tr, CompareOptions.IgnoreCase);
            if (x.GetType() == y.GetType() && x is IComparable cx)
                return cx.CompareTo(y);
            return string.Compare(
                Format(x), Format(y), Tr, CompareOptions.IgnoreCase);
        }
    }

    private static async Task<List<string>> DescribeFiltersAsync(
        AppDbContext db,
        ReportBuildRequest r,
        CancellationToken ct)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(r.Search))
            parts.Add($"Arama: {r.Search.Trim()}");
        if (r.UnitId.HasValue)
        {
            var name = await db.OrganizationUnits.AsNoTracking()
                .Where(x => x.Id == r.UnitId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"Birim: {name ?? "?"}{(r.IncludeSubUnits ? " (+ alt birimler)" : "")}");
        }
        if (r.FacilityId.HasValue)
        {
            var name = await db.OrganizationUnits.AsNoTracking()
                .Where(x => x.Id == r.FacilityId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"Tesis: {name ?? "?"}");
        }
        if (r.EmploymentTypeId.HasValue)
        {
            var name = await db.EmploymentTypes.AsNoTracking()
                .Where(x => x.Id == r.EmploymentTypeId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"İstihdam türü: {name ?? "?"}");
        }
        if (r.JobTitleId.HasValue)
        {
            var name = await db.JobTitles.AsNoTracking()
                .Where(x => x.Id == r.JobTitleId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"Unvan: {name ?? "?"}");
        }
        if (r.JobDutyId.HasValue)
        {
            var name = await db.JobDuties.AsNoTracking()
                .Where(x => x.Id == r.JobDutyId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"Fiili görev: {name ?? "?"}");
        }
        if (r.SkillId.HasValue)
        {
            var name = await db.Skills.AsNoTracking()
                .Where(x => x.Id == r.SkillId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
            parts.Add($"Yetkinlik: {name ?? "?"}");
        }
        if (r.Status.HasValue)
            parts.Add($"Durum: {EmployeeExportQuery.StatusLabel(r.Status.Value)}");
        if (r.DutyCategory.HasValue)
            parts.Add("Görev kategorisi filtresi");
        if (r.EducationLevel.HasValue)
            parts.Add($"Eğitim seviyesi: {ReportColumns.EducationLevelLabel(r.EducationLevel)}");
        if (r.IncompleteProfileOnly) parts.Add("Eksik profil bilgisi olanlar");
        if (r.MissingSkillsOnly) parts.Add("Yetkinliği girilmemiş olanlar");
        if (r.MissingPhoneOnly) parts.Add("Telefonu eksik olanlar");
        if (r.MissingFacilityOnly) parts.Add("Tesisi eksik olanlar");
        if (r.HasSpecialConditionOnly) parts.Add("Özel durumu olanlar");
        if (r.HasNotesOnly) parts.Add("Notu olanlar");
        if (r.MissingCertificatesOnly) parts.Add("Sertifikası olmayanlar");
        if (r.HasCertificatesOnly) parts.Add("Sertifikası olanlar");
        if (r.ExpiredCertificateOnly) parts.Add("Süresi dolmuş sertifikası olanlar");
        if (r.HasMovementsOnly || r.MovementFrom.HasValue || r.MovementTo.HasValue)
        {
            var range = r.MovementFrom.HasValue || r.MovementTo.HasValue
                ? $" ({r.MovementFrom?.ToString("dd.MM.yyyy") ?? "…"} – {r.MovementTo?.ToString("dd.MM.yyyy") ?? "…"})"
                : "";
            parts.Add($"Görev yeri/görevi değişenler{range}");
        }
        if (r.HireYearFrom is > 0 || r.HireYearTo is > 0)
            parts.Add($"İşe giriş yılı: {r.HireYearFrom?.ToString() ?? "…"}–{r.HireYearTo?.ToString() ?? "…"}");
        if (!string.IsNullOrWhiteSpace(r.UniversityContains))
            parts.Add($"Üniversite: {r.UniversityContains.Trim()}");
        if (r.GraduationYearFrom is > 0 || r.GraduationYearTo is > 0)
            parts.Add($"Mezuniyet yılı: {r.GraduationYearFrom?.ToString() ?? "…"}–{r.GraduationYearTo?.ToString() ?? "…"}");

        return parts;
    }
}

public sealed class GetReportColumnsHandler
    : IRequestHandler<GetReportColumnsQuery, IReadOnlyList<ReportColumnDto>>
{
    private readonly ICurrentUserService _currentUser;

    public GetReportColumnsHandler(ICurrentUserService currentUser) => _currentUser = currentUser;

    public Task<IReadOnlyList<ReportColumnDto>> Handle(
        GetReportColumnsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsView))
            throw new ForbiddenException("Raporlar için Reports.View gerekir.");

        IReadOnlyList<ReportColumnDto> result = ReportColumns
            .Permitted(_currentUser)
            .Select(ReportColumns.ToDto)
            .ToList();
        return Task.FromResult(result);
    }
}

public sealed class BuildReportHandler : IRequestHandler<BuildReportQuery, ReportResultDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IAppSettingsService _settings;

    public BuildReportHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        IAppSettingsService settings)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _settings = settings;
    }

    public async Task<ReportResultDto> Handle(BuildReportQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsView))
            throw new ForbiddenException("Raporlar için Reports.View gerekir.");

        var maxRows = await _settings.GetIntAsync(
            AppSettingKeys.ReportsExcelMaxRows,
            ExportEmployeesExcelHandler.MaxRows,
            cancellationToken);

        var built = await ReportBuildCore.BuildAsync(
            _db, _currentUser, _nationalId, request, maxRows, cancellationToken);

        return new ReportResultDto
        {
            Title = built.Title,
            GeneratedBy = _currentUser.UserName ?? "system",
            GeneratedAtUtc = DateTime.UtcNow,
            TotalCount = built.TotalCount,
            Columns = built.Columns.Select(ReportColumns.ToDto).ToList(),
            Rows = built.Rows,
            AppliedFilters = built.AppliedFilters,
            GroupByKey = built.GroupByKey,
            GroupByLabel = built.GroupByLabel
        };
    }
}

public sealed class BuildReportExcelHandler : IRequestHandler<BuildReportExcelQuery, ReportFileResult>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IAppSettingsService _settings;

    public BuildReportExcelHandler(
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
        BuildReportExcelQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsExportExcel))
            throw new ForbiddenException("Excel dışa aktarım için Reports.ExportExcel gerekir.");

        var maxRows = await _settings.GetIntAsync(
            AppSettingKeys.ReportsExcelMaxRows,
            ExportEmployeesExcelHandler.MaxRows,
            cancellationToken);

        var built = await ReportBuildCore.BuildAsync(
            _db, _currentUser, _nationalId, request, maxRows, cancellationToken);

        await ReportExportAudit.WriteAsync(
            _db, _currentUser, _notifications, "report-xlsx", built, cancellationToken);

        return BuildWorkbook(built, _currentUser.UserName ?? "system");
    }

    private static ReportFileResult BuildWorkbook(BuiltReport built, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Rapor");
        var colCount = built.Columns.Count;

        sheet.Cell(1, 1).Value = built.Title;
        sheet.Range(1, 1, 1, Math.Max(colCount, 1)).Merge().Style
            .Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value =
            $"Oluşturan: {generatedBy} · {DateTime.Now:dd.MM.yyyy HH:mm} · {built.TotalCount} kayıt";
        sheet.Range(2, 1, 2, Math.Max(colCount, 1)).Merge();
        sheet.Cell(3, 1).Value = built.AppliedFilters.Count == 0
            ? "Filtre yok (kapsam içi tümü)"
            : "Filtreler: " + string.Join(" · ", built.AppliedFilters);
        sheet.Range(3, 1, 3, Math.Max(colCount, 1)).Merge();

        var headerRow = 5;
        for (var i = 0; i < colCount; i++)
            sheet.Cell(headerRow, i + 1).Value = built.Columns[i].Label;
        sheet.Row(headerRow).Style.Font.Bold = true;
        sheet.Row(headerRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8f1fb");
        sheet.SheetView.FreezeRows(headerRow);

        var groupIdx = built.GroupByKey is null
            ? -1
            : built.Columns.ToList().FindIndex(c => c.Key == built.GroupByKey);

        var r = headerRow + 1;
        string? currentGroup = null;
        var groupStarted = false;
        foreach (var row in built.Rows)
        {
            if (groupIdx >= 0)
            {
                var value = row[groupIdx] ?? "—";
                if (!groupStarted || value != currentGroup)
                {
                    currentGroup = value;
                    groupStarted = true;
                    var count = built.Rows.Count(x => (x[groupIdx] ?? "—") == value);
                    sheet.Cell(r, 1).Value = $"{built.GroupByLabel}: {value} ({count} kayıt)";
                    var range = sheet.Range(r, 1, r, colCount).Merge();
                    range.Style.Font.SetBold();
                    range.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#f0f4f8"));
                    r++;
                }
            }

            for (var i = 0; i < colCount; i++)
                sheet.Cell(r, i + 1).Value = row[i] ?? "";
            r++;
        }

        sheet.Columns().AdjustToContents(headerRow, Math.Max(r - 1, headerRow));

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ReportFileResult
        {
            Content = ms.ToArray(),
            FileName = $"rapor-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx"
        };
    }
}

public sealed class BuildReportPdfHandler : IRequestHandler<BuildReportPdfQuery, ReportFileResult>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IAppSettingsService _settings;

    static BuildReportPdfHandler()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public BuildReportPdfHandler(
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
        BuildReportPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsExportPdf))
            throw new ForbiddenException("PDF dışa aktarım için Reports.ExportPdf gerekir.");

        var maxRows = await _settings.GetIntAsync(
            AppSettingKeys.ReportsPdfMaxRows,
            ExportEmployeesPdfHandler.MaxRows,
            cancellationToken);
        var orgName = await _settings.GetStringAsync(
            AppSettingKeys.OrganizationDisplayName,
            "Şehitkamil Kültür Müdürlüğü",
            cancellationToken);

        var built = await ReportBuildCore.BuildAsync(
            _db, _currentUser, _nationalId, request, maxRows, cancellationToken);

        await ReportExportAudit.WriteAsync(
            _db, _currentUser, _notifications, "report-pdf", built, cancellationToken);

        var bytes = BuildPdf(built, _currentUser.UserName ?? "system", orgName);

        return new ReportFileResult
        {
            Content = bytes,
            FileName = $"rapor-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
            ContentType = "application/pdf"
        };
    }

    private static byte[]? TryLoadLogo()
    {
        foreach (var basePath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var path = Path.Combine(basePath, "wwwroot", "logo.png");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }
        return null;
    }

    private static byte[] BuildPdf(BuiltReport built, string generatedBy, string orgName)
    {
        var logo = TryLoadLogo();
        var filterText = built.AppliedFilters.Count == 0
            ? "Filtre yok (kapsam içi tümü)"
            : string.Join(" · ", built.AppliedFilters);

        var groupIdx = built.GroupByKey is null
            ? -1
            : built.Columns.ToList().FindIndex(c => c.Key == built.GroupByKey);

        // Gruplama varsa satırları bölümlere ayır (grup sütunu tabloda tekrar etmez)
        var sections = new List<(string? Header, List<IReadOnlyList<string?>> Rows)>();
        if (groupIdx >= 0)
        {
            foreach (var row in built.Rows)
            {
                var value = row[groupIdx] ?? "—";
                if (sections.Count == 0 || sections[^1].Header != value)
                    sections.Add((value, []));
                sections[^1].Rows.Add(row);
            }
        }
        else
        {
            sections.Add((null, built.Rows.ToList()));
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(built.Columns.Count > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        if (logo is not null)
                            row.ConstantItem(46).Height(46).Image(logo).FitArea();
                        row.RelativeItem().PaddingLeft(logo is not null ? 10 : 0).Column(head =>
                        {
                            head.Item().Text("T.C. ŞEHİTKAMİL BELEDİYESİ")
                                .FontSize(9).FontColor(Colors.Grey.Darken2).SemiBold();
                            head.Item().Text(orgName).SemiBold().FontSize(12);
                            head.Item().Text(built.Title).Bold().FontSize(15);
                        });
                        row.ConstantItem(150).AlignRight().Column(meta =>
                        {
                            meta.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}")
                                .FontSize(8).FontColor(Colors.Grey.Darken2);
                            meta.Item().Text($"Oluşturan: {generatedBy}")
                                .FontSize(8).FontColor(Colors.Grey.Darken2);
                            meta.Item().Text($"{built.TotalCount} kayıt")
                                .FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                    col.Item().PaddingTop(4).Text(filterText)
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(8).Column(body =>
                {
                    foreach (var (header, rows) in sections)
                    {
                        if (header is not null)
                        {
                            body.Item().PaddingTop(6).PaddingBottom(2)
                                .Text($"{built.GroupByLabel}: {header} ({rows.Count} kayıt)")
                                .SemiBold().FontSize(10).FontColor(Colors.Blue.Darken3);
                        }

                        body.Item().Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                foreach (var c in built.Columns)
                                    def.RelativeColumn(ColumnWeight(c.Key));
                            });

                            table.Header(h =>
                            {
                                foreach (var c in built.Columns)
                                    h.Cell().Element(CellHeader).Text(c.Label).SemiBold();
                            });

                            foreach (var row in rows)
                            {
                                foreach (var cell in row)
                                    table.Cell().Element(CellBody).Text(cell ?? "—");
                            }
                        });
                    }

                    if (built.TotalCount == 0)
                        body.Item().PaddingTop(10).Text("Kayıt bulunamadı.")
                            .FontColor(Colors.Grey.Darken1);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{orgName} · Personel Bilgi ve Yönetim Sistemi")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(120).AlignRight().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });
        });

        return doc.GeneratePdf();

        static float ColumnWeight(string key) => key switch
        {
            "fullName" or "skills" or "certificates" or "address" or "lastMovement"
                or "university" or "department" or "email" => 1.7f,
            "unit" or "facility" or "jobTitle" or "duty" => 1.3f,
            _ => 1f
        };

        static IContainer CellHeader(IContainer c) =>
            c.BorderBottom(1).BorderColor(Colors.Grey.Medium)
                .PaddingVertical(4).PaddingHorizontal(3)
                .Background(Colors.Grey.Lighten3);

        static IContainer CellBody(IContainer c) =>
            c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(3).PaddingHorizontal(3);
    }
}

/// <summary>Export'lar SaveChanges üretmez — ortak manuel audit + bildirim.</summary>
internal static class ReportExportAudit
{
    public static async Task WriteAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        IUserNotificationService notifications,
        string format,
        BuiltReport built,
        CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = "Export",
            EntityName = "Employee",
            EntityId = null,
            UserId = currentUser.UserId?.ToString(),
            UserName = currentUser.UserName ?? "system",
            NewValuesJson = JsonSerializer.Serialize(new
            {
                format,
                title = built.Title,
                rowCount = built.TotalCount,
                columns = built.Columns.Select(c => c.Key).ToArray(),
                groupBy = built.GroupByKey,
                filters = built.AppliedFilters
            })
        });
        await db.SaveChangesAsync(ct);

        if (currentUser.UserId is Guid uid)
        {
            await notifications.NotifyAsync(
                uid,
                "Rapor dışa aktarımı tamamlandı",
                $"“{built.Title}” raporu ({built.TotalCount} satır) oluşturuldu.",
                NotificationSeverity.Info,
                "Export",
                "/reports",
                ct);
        }
    }
}
