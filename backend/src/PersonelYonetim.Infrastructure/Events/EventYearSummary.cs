using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Events;

public static class EventYearStatistics
{
    public static async Task<EventYearSummaryDto> CalculateAsync(IQueryable<Event> events, int year, CancellationToken ct)
    {
        if (year is < 1900 or > 2100) throw new ValidationException("year", "1900 ile 2100 arasında bir yıl seçin.");
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-3);
        var to = from.AddYears(1);
        var now = DateTime.UtcNow;
        var rows = await events.Where(e => e.StartAtUtc >= from && e.StartAtUtc < to
            && (e.Status == EventStatus.Completed || e.Status == EventStatus.Published && e.StartAtUtc < now))
            .Select(e => new { e.Category, e.ActualAttendees,
                SettlementAttendance = e.Settlements.Where(s => !s.IsDeleted).Sum(s => (long?)s.AttendanceCount) ?? 0 })
            .ToListAsync(ct);
        var attendance = rows.Select(e => e.ActualAttendees.HasValue ? (long?)e.ActualAttendees.Value
            : e.SettlementAttendance > 0 ? e.SettlementAttendance : null).ToList();
        return new(year, rows.Count(e => e.Category != EventCategories.Project),
            rows.Count(e => e.Category == EventCategories.Project), attendance.Sum(n => n ?? 0), attendance.Count(n => n.HasValue));
    }
}

public sealed class GetEventYearSummaryHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<GetEventYearSummaryQuery, EventYearSummaryDto>
{
    public async Task<EventYearSummaryDto> Handle(GetEventYearSummaryQuery request, CancellationToken ct)
    {
        if (!user.HasPermission(PermissionCodes.EventsView)) throw new ForbiddenException("Etkinlikleri görüntüleme yetkiniz yok.");
        var events = db.Events.AsNoTracking();
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (allowed is not null) events = events.Where(e => e.FacilityId != null && allowed.Contains(e.FacilityId.Value));
        if (request.FacilityId is Guid facility)
        {
            var ids = await db.OrganizationUnits.Where(u => u.Id == facility || u.ParentId == facility).Select(u => u.Id).ToListAsync(ct);
            events = events.Where(e => e.FacilityId != null && ids.Contains(e.FacilityId.Value));
        }
        return await EventYearStatistics.CalculateAsync(events, request.Year, ct);
    }
}
