using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Events;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Events;

public sealed class GetHallBoardHandler : IRequestHandler<GetHallBoardQuery, HallBoardDto>
{
    private static readonly HashSet<string> ExtraVenueCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SANAT", "NIKAH", "DTSS"
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetHallBoardHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<HallBoardDto> Handle(GetHallBoardQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Salon tahsisini görüntüleme yetkiniz yok.");

        var from = DateTime.SpecifyKind(request.FromUtc ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.ToUtc ?? from.AddDays(7), DateTimeKind.Utc);
        if (to <= from)
            to = from.AddDays(7);

        var kkm = await _db.OrganizationUnits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "KKM" || x.Code == "FAC_KKM", cancellationToken);

        var units = await _db.OrganizationUnits.AsNoTracking()
            .Where(x =>
                x.Type == OrganizationUnitType.Facility
                && x.Status != OrganizationUnitStatus.Closed
                && x.Status != OrganizationUnitStatus.OutOfUse
                && (
                    (kkm != null && x.ParentId == kkm.Id)
                    || (x.Code != null && ExtraVenueCodes.Contains(x.Code))))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
                x.ParentId,
                ParentName = x.Parent != null ? x.Parent.Name : null,
                x.Capacity
            })
            .ToListAsync(cancellationToken);

        var halls = units
            .OrderBy(x => kkm != null && x.ParentId == kkm.Id ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
            .ToList();

        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (allowed is not null)
            halls = halls.Where(x => allowed.Contains(x.Id)).ToList();

        var hallIds = halls.Select(x => x.Id).ToList();
        if (kkm is not null && (allowed is null || allowed.Contains(kkm.Id)))
            hallIds.Add(kkm.Id);

        var now = DateTime.UtcNow;
        var lastDoneFrom = now.AddMonths(-24);
        var nextUntil = now.AddYears(2);
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.FacilityId != null && hallIds.Contains(e.FacilityId.Value) && e.Status != EventStatus.Cancelled)
            .Where(e =>
                (e.StartAtUtc < to && (e.EndAtUtc ?? e.StartAtUtc) >= from)
                || (e.Status == EventStatus.Published && e.StartAtUtc >= now && e.StartAtUtc < nextUntil)
                || (e.Status == EventStatus.Completed && e.StartAtUtc <= now && e.StartAtUtc >= lastDoneFrom)
                || (e.StartAtUtc <= now && (e.EndAtUtc ?? e.StartAtUtc.AddHours(2)) >= now))
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Status,
                e.StartAtUtc,
                e.EndAtUtc,
                e.FacilityId,
                e.ExpectedAttendees,
                e.ActualAttendees,
                SettlementSum = e.Settlements.Sum(s => s.AttendanceCount)
            })
            .ToListAsync(cancellationToken);

        HallBookingDto ToDto(
            Guid id,
            string title,
            EventStatus status,
            DateTime start,
            DateTime? end,
            int? expected,
            int? actual,
            int settlementSum) => new()
        {
            Id = id,
            Title = title,
            Status = status,
            StatusLabel = EventLabels.Status(status),
            StartAtUtc = start,
            EndAtUtc = end,
            ExpectedAttendees = expected,
            ActualAttendees = actual,
            AttendanceCount = EventAttendance.Resolve(actual, settlementSum, expected)
        };

        var rows = new List<HallBoardItemDto>();
        foreach (var hall in halls)
        {
            var mine = events.Where(e => e.FacilityId == hall.Id).ToList();

            var inRange = mine
                .Where(e => e.StartAtUtc < to && (e.EndAtUtc ?? e.StartAtUtc.AddHours(2)) >= from)
                .OrderBy(e => e.StartAtUtc)
                .ToList();

            var lastDone = mine
                .Where(e => e.Status == EventStatus.Completed && e.StartAtUtc <= now)
                .OrderByDescending(e => e.StartAtUtc)
                .FirstOrDefault();

            var next = mine
                .Where(e => e.Status == EventStatus.Published && e.StartAtUtc >= now)
                .OrderBy(e => e.StartAtUtc)
                .FirstOrDefault();

            var occupied = mine.Any(e =>
                e.Status is EventStatus.Published or EventStatus.Completed
                && e.StartAtUtc <= now
                && (e.EndAtUtc ?? e.StartAtUtc.AddHours(2)) >= now);

            rows.Add(new HallBoardItemDto
            {
                FacilityId = hall.Id,
                Name = hall.Name,
                ParentName = hall.ParentName,
                Capacity = hall.Capacity,
                Group = kkm is not null && hall.ParentId == kkm.Id ? "kkm" : "other",
                OccupiedNow = occupied,
                LastDone = lastDone is null
                    ? null
                    : ToDto(lastDone.Id, lastDone.Title, lastDone.Status, lastDone.StartAtUtc, lastDone.EndAtUtc, lastDone.ExpectedAttendees, lastDone.ActualAttendees, lastDone.SettlementSum),
                Next = next is null
                    ? null
                    : ToDto(next.Id, next.Title, next.Status, next.StartAtUtc, next.EndAtUtc, next.ExpectedAttendees, next.ActualAttendees, next.SettlementSum),
                Bookings = inRange
                    .Select(e => ToDto(e.Id, e.Title, e.Status, e.StartAtUtc, e.EndAtUtc, e.ExpectedAttendees, e.ActualAttendees, e.SettlementSum))
                    .ToList()
            });
        }

        return new HallBoardDto { FromUtc = from, ToUtc = to, Halls = rows };
    }
}
