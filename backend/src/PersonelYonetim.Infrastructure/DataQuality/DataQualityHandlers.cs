using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.DataQuality;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Reports;

namespace PersonelYonetim.Infrastructure.DataQuality;

/// <summary>
/// Veri kalitesi raporu: eksik alanlar + tutarsızlıklar + tamamlanma bantları.
/// </summary>
public sealed class GetDataQualityReportHandler
    : IRequestHandler<GetDataQualityReportQuery, DataQualityReportDto>
{
    private static readonly IReadOnlyList<DataQualityRuleDto> Rules =
    [
        // —— Eksikler ——
        Rule(DataQualityIssueCodes.MissingPhone, "Telefon yok",
            "Kişisel veya kurumsal telefon girilmemiş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingEducation, "Eğitim kaydı yok",
            "Hiç eğitim satırı yok.", DataQualityIssueKinds.Missing, false),
        Rule(DataQualityIssueCodes.MissingHireDate, "İşe giriş tarihi yok",
            "Belediyede işe giriş tarihi boş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingSkills, "Yetkinlik kaydı yok",
            "Hiç yetkinlik atanmamış.", DataQualityIssueKinds.Missing, false),
        Rule(DataQualityIssueCodes.MissingFacility, "Tesis belirlenmemiş",
            "Çalıştığı tesis alanı boş.", DataQualityIssueKinds.Missing, false),
        Rule(DataQualityIssueCodes.MissingJobTitle, "Resmi unvan yok",
            "Resmi unvan seçilmemiş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingUnit, "Birim yok",
            "Organizasyon birimi atanmamış.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingEmployeeNumber, "Sicil no yok",
            "Personel numarası / sicil boş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingBirthDate, "Doğum tarihi yok",
            "Doğum tarihi girilmemiş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingGender, "Cinsiyet belirsiz",
            "Cinsiyet seçilmemiş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingEmail, "E-posta yok",
            "Kişisel veya kurumsal e-posta yok.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingAddress, "Adres yok",
            "Adres alanı boş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingEmploymentType, "İstihdam türü yok",
            "Kadrolu / sözleşmeli vb. seçilmemiş.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingNationalId, "TCKN yok",
            "Kimlik numarası kaydı yok.", DataQualityIssueKinds.Missing, true),
        Rule(DataQualityIssueCodes.MissingPrimaryAssignment, "Ana görev yok",
            "Aktif ana görev ataması yok.", DataQualityIssueKinds.Missing, false),
        Rule(DataQualityIssueCodes.MissingEmergencyContact, "Acil durum kişisi yok",
            "Acil iletişim adı veya telefonu eksik.", DataQualityIssueKinds.Missing, false),

        // —— Tutarsızlıklar ——
        Rule(DataQualityIssueCodes.DuplicatePhone, "Mükerrer telefon",
            "Aynı telefon numarası birden fazla personele kayıtlı.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.DuplicateEmail, "Mükerrer e-posta",
            "Aynı e-posta adresi birden fazla personele kayıtlı.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.FutureHireDate, "İşe giriş gelecekte",
            "İşe giriş tarihi bugünden sonra görünüyor.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.UnitStartBeforeHire, "Birim tarihi işe girişten önce",
            "Birime başlangıç tarihi işe giriş tarihinden önce.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.ActiveInPassiveFacility, "Pasif tesiste aktif personel",
            "Personel aktif ama atandığı tesis pasif/kapalı.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.InactiveUnit, "Birim bilgisi hatalı",
            "Personelin birimi pasif, kapalı veya geçersiz.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.MultiplePrimaryDuties, "Birden fazla ana görev",
            "Aynı anda iki veya daha fazla aktif ana görev tanımlı.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.ActiveMissingFacility, "Aktif personelde tesis yok",
            "Aktif personelin çalıştığı tesis tanımlanmamış.", DataQualityIssueKinds.Inconsistency, false),
        Rule(DataQualityIssueCodes.EmptyDutyCategory, "Görev kategorisi boş",
            "Aktif görevin kategorisi tanımsız veya Other.", DataQualityIssueKinds.Inconsistency, false)
    ];

    private static readonly IReadOnlyList<(string Key, string Label, byte Min, byte Max)> Bands =
    [
        ("full", "%100 — Tüm zorunlu bilgiler tamam", 100, 100),
        ("high", "%80–99 — İkincil bilgiler eksik", 80, 99),
        ("mid", "%60–79 — Eğitim / yetkinlik eksik", 60, 79),
        ("low", "%40–59 — İletişim veya işe giriş eksik", 40, 59),
        ("critical", "%20–39 — Temel kurumsal bilgiler eksik", 20, 39),
        ("minimal", "%0–19 — Yalnızca isim / görev seviyesi", 0, 19)
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDataQualityReportHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DataQualityReportDto> Handle(
        GetDataQualityReportQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.DataQualityView))
            throw new ForbiddenException("Veri kalitesi için DataQuality.View gerekir.");

        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Personel listesi için Employees.View gerekir.");

        var query = _db.Employees
            .AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Facility)
            .Include(x => x.SensitiveData)
            .Include(x => x.Assignments).ThenInclude(a => a.JobDuty)
            .Include(x => x.EducationRecords)
            .Include(x => x.Skills)
            .AsQueryable();

        var scoped = await UnitScopeHelper.ApplyAsync(_db, _currentUser, query, cancellationToken);
        if (scoped is null)
        {
            return EmptyReport();
        }

        query = scoped;

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status);

        var employees = await query
            .OrderBy(x => x.ProfileCompletionPercent)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var duplicatePhones = BuildDuplicateKeys(employees, PhoneKeys);
        var duplicateEmails = BuildDuplicateKeys(employees, EmailKeys);

        var labelByCode = Rules.ToDictionary(r => r.Code, r => r.Label, StringComparer.Ordinal);
        var kindByCode = Rules.ToDictionary(r => r.Code, r => r.Kind, StringComparer.Ordinal);
        var issueCounts = Rules.ToDictionary(r => r.Code, _ => 0, StringComparer.Ordinal);

        var detected = new List<(Employee Emp, List<string> Issues)>(employees.Count);
        var withAny = 0;
        var withMissing = 0;
        var withInconsistency = 0;

        foreach (var e in employees)
        {
            var issues = DetectIssues(e, today, duplicatePhones, duplicateEmails);
            if (issues.Count == 0)
                continue;

            withAny++;
            var hasMissing = false;
            var hasInconsistency = false;
            foreach (var code in issues)
            {
                if (issueCounts.ContainsKey(code))
                    issueCounts[code]++;
                if (kindByCode.TryGetValue(code, out var kind))
                {
                    if (kind == DataQualityIssueKinds.Missing) hasMissing = true;
                    else hasInconsistency = true;
                }
            }

            if (hasMissing) withMissing++;
            if (hasInconsistency) withInconsistency++;
            detected.Add((e, issues));
        }

        var kindFilter = NormalizeKind(request.Kind);
        var bandFilter = NormalizeBand(request.CompletionBand);

        var rows = new List<DataQualityEmployeeDto>();
        foreach (var (e, issues) in detected)
        {
            if (request.MaxCompletionPercent.HasValue
                && e.ProfileCompletionPercent > request.MaxCompletionPercent.Value)
                continue;

            if (bandFilter is not null
                && !InBand(e.ProfileCompletionPercent, bandFilter.Value.Min, bandFilter.Value.Max))
                continue;

            if (!string.IsNullOrWhiteSpace(request.IssueCode)
                && !issues.Contains(request.IssueCode, StringComparer.Ordinal))
                continue;

            if (kindFilter is not null)
            {
                var matchKind = issues.Any(c =>
                    kindByCode.TryGetValue(c, out var k) && k == kindFilter);
                if (!matchKind)
                    continue;
            }

            var missing = issues
                .Where(c => kindByCode.TryGetValue(c, out var k) && k == DataQualityIssueKinds.Missing)
                .ToList();
            var inconsist = issues
                .Where(c => kindByCode.TryGetValue(c, out var k) && k == DataQualityIssueKinds.Inconsistency)
                .ToList();

            rows.Add(new DataQualityEmployeeDto
            {
                Id = e.Id,
                FullName = e.FullName,
                EmployeeNumber = e.EmployeeNumber,
                UnitName = e.Unit?.Name,
                FacilityName = e.Facility?.Name,
                Status = e.Status,
                ProfileCompletionPercent = e.ProfileCompletionPercent,
                IssueCodes = issues,
                IssueLabels = issues
                    .Select(c => labelByCode.TryGetValue(c, out var l) ? l : c)
                    .ToList(),
                MissingCodes = missing,
                InconsistencyCodes = inconsist
            });
        }

        // Listeyi makul boyutta tut (istatistikler tüm kapsamdadır).
        if (rows.Count > 500)
            rows = rows.Take(500).ToList();

        var avg = employees.Count == 0
            ? 0
            : Math.Round(employees.Average(x => (double)x.ProfileCompletionPercent), 1);

        var bands = Bands
            .Select(b => new DataQualityCompletionBandDto
            {
                Key = b.Key,
                Label = b.Label,
                MinPercent = b.Min,
                MaxPercent = b.Max,
                Count = employees.Count(e => InBand(e.ProfileCompletionPercent, b.Min, b.Max))
            })
            .ToList();

        return new DataQualityReportDto
        {
            ScopedEmployeeCount = employees.Count,
            EmployeesWithIssues = withAny,
            MissingIssueEmployeeCount = withMissing,
            InconsistencyEmployeeCount = withInconsistency,
            AverageCompletionPercent = avg,
            CompletionBands = bands,
            IssueStats = Rules
                .Select(r => new DataQualityIssueStatDto
                {
                    Code = r.Code,
                    Label = r.Label,
                    Kind = r.Kind,
                    Count = issueCounts[r.Code]
                })
                .Where(s => s.Count > 0)
                .OrderByDescending(s => s.Count)
                .ToList(),
            Employees = rows,
            Rules = Rules
        };
    }

    private static DataQualityReportDto EmptyReport() => new()
    {
        Rules = Rules,
        IssueStats = [],
        Employees = [],
        CompletionBands = Bands
            .Select(b => new DataQualityCompletionBandDto
            {
                Key = b.Key,
                Label = b.Label,
                MinPercent = b.Min,
                MaxPercent = b.Max,
                Count = 0
            })
            .ToList()
    };

    private static DataQualityRuleDto Rule(
        string code, string label, string description, string kind, bool affectsScore) =>
        new()
        {
            Code = code,
            Label = label,
            Description = description,
            Kind = kind,
            AffectsCompletionScore = affectsScore
        };

    private static List<string> DetectIssues(
        Employee e,
        DateOnly today,
        HashSet<string> duplicatePhones,
        HashSet<string> duplicateEmails)
    {
        var issues = new List<string>();

        // —— Eksikler ——
        if (string.IsNullOrWhiteSpace(e.EmployeeNumber))
            issues.Add(DataQualityIssueCodes.MissingEmployeeNumber);
        if (!e.BirthDate.HasValue)
            issues.Add(DataQualityIssueCodes.MissingBirthDate);
        if (e.Gender == Gender.Unspecified)
            issues.Add(DataQualityIssueCodes.MissingGender);
        if (string.IsNullOrWhiteSpace(e.PersonalPhone) && string.IsNullOrWhiteSpace(e.CorporatePhone))
            issues.Add(DataQualityIssueCodes.MissingPhone);
        if (string.IsNullOrWhiteSpace(e.PersonalEmail) && string.IsNullOrWhiteSpace(e.CorporateEmail))
            issues.Add(DataQualityIssueCodes.MissingEmail);
        if (string.IsNullOrWhiteSpace(e.Address))
            issues.Add(DataQualityIssueCodes.MissingAddress);
        if (!e.UnitId.HasValue)
            issues.Add(DataQualityIssueCodes.MissingUnit);
        if (!e.FacilityId.HasValue)
            issues.Add(DataQualityIssueCodes.MissingFacility);
        if (!e.EmploymentTypeId.HasValue)
            issues.Add(DataQualityIssueCodes.MissingEmploymentType);
        if (!e.JobTitleId.HasValue)
            issues.Add(DataQualityIssueCodes.MissingJobTitle);
        if (!e.HireDate.HasValue)
            issues.Add(DataQualityIssueCodes.MissingHireDate);
        if (string.IsNullOrWhiteSpace(e.SensitiveData?.NationalIdEncrypted))
            issues.Add(DataQualityIssueCodes.MissingNationalId);

        var activeAssignments = e.Assignments.Where(a => a.EndDate is null).ToList();
        var primaryCount = activeAssignments.Count(a => a.IsPrimary);
        if (primaryCount == 0)
            issues.Add(DataQualityIssueCodes.MissingPrimaryAssignment);

        if (e.EducationRecords.Count == 0)
            issues.Add(DataQualityIssueCodes.MissingEducation);
        if (e.Skills.Count == 0)
            issues.Add(DataQualityIssueCodes.MissingSkills);

        if (string.IsNullOrWhiteSpace(e.EmergencyContactName)
            || string.IsNullOrWhiteSpace(e.EmergencyContactPhone))
            issues.Add(DataQualityIssueCodes.MissingEmergencyContact);

        // —— Tutarsızlıklar ——
        foreach (var phone in PhoneKeys(e))
        {
            if (duplicatePhones.Contains(phone))
            {
                issues.Add(DataQualityIssueCodes.DuplicatePhone);
                break;
            }
        }

        foreach (var email in EmailKeys(e))
        {
            if (duplicateEmails.Contains(email))
            {
                issues.Add(DataQualityIssueCodes.DuplicateEmail);
                break;
            }
        }

        if (e.HireDate.HasValue && e.HireDate.Value > today)
            issues.Add(DataQualityIssueCodes.FutureHireDate);

        if (e.HireDate.HasValue && e.UnitStartDate.HasValue && e.UnitStartDate.Value < e.HireDate.Value)
            issues.Add(DataQualityIssueCodes.UnitStartBeforeHire);

        if (e.Status == EmployeeStatus.Active && e.Facility is not null
            && e.Facility.Status != OrganizationUnitStatus.Active)
            issues.Add(DataQualityIssueCodes.ActiveInPassiveFacility);

        if (e.UnitId.HasValue && e.Unit is not null
            && e.Unit.Status != OrganizationUnitStatus.Active)
            issues.Add(DataQualityIssueCodes.InactiveUnit);

        if (primaryCount > 1)
            issues.Add(DataQualityIssueCodes.MultiplePrimaryDuties);

        if (e.Status == EmployeeStatus.Active && !e.FacilityId.HasValue)
            issues.Add(DataQualityIssueCodes.ActiveMissingFacility);

        // Aktif ana görevde kategori Other veya tanımsız sayılırsa uyarı.
        var primaryDuty = activeAssignments.FirstOrDefault(a => a.IsPrimary)?.JobDuty;
        if (primaryDuty is not null && primaryDuty.Category == DutyCategory.Other)
            issues.Add(DataQualityIssueCodes.EmptyDutyCategory);

        return issues;
    }

    private static HashSet<string> BuildDuplicateKeys(
        IEnumerable<Employee> employees,
        Func<Employee, IEnumerable<string>> keySelector)
    {
        return employees
            .SelectMany(keySelector)
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> PhoneKeys(Employee e)
    {
        foreach (var raw in new[] { e.PersonalPhone, e.CorporatePhone })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length >= 7)
                yield return digits;
        }
    }

    private static IEnumerable<string> EmailKeys(Employee e)
    {
        foreach (var raw in new[] { e.PersonalEmail, e.CorporateEmail })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            yield return raw.Trim().ToLowerInvariant();
        }
    }

    private static bool InBand(byte percent, byte min, byte max) =>
        percent >= min && percent <= max;

    private static string? NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return null;
        var k = kind.Trim().ToLowerInvariant();
        return k is DataQualityIssueKinds.Missing or DataQualityIssueKinds.Inconsistency
            ? k
            : null;
    }

    private static (byte Min, byte Max)? NormalizeBand(string? band)
    {
        if (string.IsNullOrWhiteSpace(band)) return null;
        var match = Bands.FirstOrDefault(b =>
            string.Equals(b.Key, band.Trim(), StringComparison.OrdinalIgnoreCase));
        return match.Key is null ? null : (match.Min, match.Max);
    }
}
