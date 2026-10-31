using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Reports;

public sealed record ReportSummaryRow(string Key, string Name, string Category, int RecordCount, long? Attendance);
public sealed record ReportMonth(int Year, int Month, int RecordCount, long Attendance);
public sealed record ReportSummary(string Type, DateOnly FromDate, DateOnly ToDate, int RecordCount, long Attendance, int RecordsWithAttendance, int EventCount, int ProjectCount, DateTime? SourceUpdatedAtUtc, IReadOnlyList<ReportSummaryRow> Rows, IReadOnlyList<ReportMonth> Months);

public sealed class ReportSummaryService(AppDbContext db, ICurrentUserService user)
{
    public async Task<ReportSummary> GetAsync(string type, DateOnly fromDate, DateOnly toDate, string kind = "all", CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.EventsView) || !user.HasPermission(PermissionCodes.EmployeesViewAllUnits)) throw new ForbiddenException("Müdürlük kapsamı ve etkinlik görüntüleme yetkisi gerekir.");
        if (type is not ("facility" or "event") || kind is not ("all" or "event" or "project") || fromDate.Year < 2017 || toDate.Year > 2100 || fromDate > toDate || toDate.DayNumber - fromDate.DayNumber > 5 * 366)
            throw new ValidationException("period", "Geçerli bir rapor türü ve en fazla 5 yıllık tarih aralığı seçin.");
        if (type == "facility")
        {
            if (fromDate.Day != 1 || toDate.Day != DateTime.DaysInMonth(toDate.Year, toDate.Month)) throw new ValidationException("period", "Tesis istatistikleri aylıktır. Başlangıç ve bitiş ayını tam olarak seçin.");
            var first = fromDate.Year * 100 + fromDate.Month; var last = toDate.Year * 100 + toDate.Month;
            var data = await db.FacilityUsageRecords.AsNoTracking().Where(r => r.Year * 100 + r.Month >= first && r.Year * 100 + r.Month <= last)
                .Select(r => new { r.FacilityId, r.Facility.Name, r.Year, r.Month, r.ParticipantCount, r.CreatedAtUtc }).ToListAsync(ct);
            var updated = await db.FacilityUsageRecords.MaxAsync(r => (DateTime?)r.CreatedAtUtc, ct);
            var rows = data.GroupBy(r => new { r.FacilityId, r.Name }).Select(g => new ReportSummaryRow(g.Key.FacilityId.ToString(), g.Key.Name, "Tesis", g.Count(), g.Sum(r => r.ParticipantCount))).OrderByDescending(r => r.Attendance).ThenBy(r => r.Name).ToList();
            var months = data.GroupBy(r => new { r.Year, r.Month }).Select(g => new ReportMonth(g.Key.Year, g.Key.Month, g.Count(), g.Sum(r => r.ParticipantCount))).OrderBy(r => r.Year).ThenBy(r => r.Month).ToList();
            return new(type, fromDate, toDate, data.Count, data.Sum(r => r.ParticipantCount), data.Count, 0, 0, updated, rows, months);
        }
        var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-3);
        var until = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-3); var now = DateTime.UtcNow;
        var query = db.Events.AsNoTracking().Where(e => e.StartAtUtc >= from && e.StartAtUtc < until && (e.Status == EventStatus.Completed || e.Status == EventStatus.Published && e.StartAtUtc < now));
        if (kind == "event") query = query.Where(e => e.Category != EventCategories.Project);
        if (kind == "project") query = query.Where(e => e.Category == EventCategories.Project);
        var events = await query.Select(e => new { e.Title, e.Category, e.CategoryName, e.StartAtUtc, e.ActualAttendees, Sum = e.Settlements.Where(s => !s.IsDeleted).Sum(s => (long?)s.AttendanceCount) ?? 0 }).ToListAsync(ct);
        var values = events.Select(e => new { e.Title, e.Category, e.CategoryName, Day = e.StartAtUtc.AddHours(3), Attendance = e.ActualAttendees.HasValue ? (long?)e.ActualAttendees.Value : e.Sum > 0 ? e.Sum : null }).ToList();
        string Title(string title, int year) => System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(title.Normalize().Trim(), @"\s+", " "), @"\s+" + year + "$", "").Trim();
        var grouped = values.GroupBy(e => new { Name = Title(e.Title, e.Day.Year).ToLower(CultureInfo.GetCultureInfo("tr-TR")), e.Category, CategoryName = e.CategoryName?.ToLower(CultureInfo.GetCultureInfo("tr-TR")), e.Day.Year });
        var result = grouped.Select(g => new ReportSummaryRow($"{g.Key.Category}:{g.Key.CategoryName}:{g.Key.Year}:{g.Key.Name}", Title(g.First().Title, g.Key.Year) + " " + g.Key.Year, EventCategories.Label(g.Key.Category ?? "other", g.First().CategoryName), g.Count(), g.Any(e => e.Attendance.HasValue) ? g.Sum(e => e.Attendance ?? 0) : null)).OrderByDescending(r => r.Attendance).ThenBy(r => r.Name).ToList();
        var monthly = values.GroupBy(e => new { e.Day.Year, e.Day.Month }).Select(g => new ReportMonth(g.Key.Year, g.Key.Month, g.Count(), g.Sum(e => e.Attendance ?? 0))).OrderBy(r => r.Year).ThenBy(r => r.Month).ToList();
        return new(type, fromDate, toDate, values.Count, values.Sum(e => e.Attendance ?? 0), values.Count(e => e.Attendance.HasValue), values.Count(e => e.Category != EventCategories.Project), values.Count(e => e.Category == EventCategories.Project), null, result, monthly);
    }
    public async Task<byte[]> WordAsync(string type, DateOnly fromDate, DateOnly toDate, string kind = "all", CancellationToken ct = default) => ReportWordDocument.Summary(await GetAsync(type, fromDate, toDate, kind, ct));
}
