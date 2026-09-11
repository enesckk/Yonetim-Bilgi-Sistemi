using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Features.Notifications;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Events;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Events;

internal static class EventLabels
{
    public static string Status(EventStatus s) => s switch
    {
        EventStatus.Draft => "Taslak",
        EventStatus.Published => "Planlandı",
        EventStatus.Cancelled => "İptal",
        EventStatus.Completed => "Yapıldı",
        _ => s.ToString()
    };

    public static string? Recurrence(EventRecurrenceFrequency f) => f switch
    {
        EventRecurrenceFrequency.Weekly => "Haftalık",
        EventRecurrenceFrequency.Monthly => "Aylık",
        _ => null
    };
}

internal static class EventConflictHelper
{
    public static DateTime EffectiveEnd(DateTime start, DateTime? end) =>
        end ?? start.AddHours(2);

    public static async Task EnsureNoFacilityConflictsAsync(
        AppDbContext db,
        Guid? facilityId,
        DateTime startAtUtc,
        DateTime? endAtUtc,
        Guid? excludeEventId,
        bool allowConflicts,
        CancellationToken ct)
    {
        if (allowConflicts || facilityId is null)
            return;

        var conflicts = await FindConflictsAsync(db, facilityId, startAtUtc, endAtUtc, excludeEventId, ct);
        if (conflicts.Count == 0)
            return;

        var titles = string.Join(", ", conflicts.Take(3).Select(c => $"«{c.Title}»"));
        var more = conflicts.Count > 3 ? $" (+{conflicts.Count - 3})" : "";
        throw new ConflictException(
            $"Bu tesiste çakışan etkinlik var: {titles}{more}. Onaylayarak yine de kaydedebilirsiniz.");
    }

    public static async Task<List<EventConflictItemDto>> FindConflictsAsync(
        AppDbContext db,
        Guid? facilityId,
        DateTime startAtUtc,
        DateTime? endAtUtc,
        Guid? excludeEventId,
        CancellationToken ct)
    {
        if (facilityId is null)
            return [];

        var start = DateTime.SpecifyKind(startAtUtc, DateTimeKind.Utc);
        var end = EffectiveEnd(start, endAtUtc is null ? null : DateTime.SpecifyKind(endAtUtc.Value, DateTimeKind.Utc));

        var candidates = await db.Events.AsNoTracking()
            .Where(e => e.FacilityId == facilityId
                && e.Status != EventStatus.Cancelled
                && (excludeEventId == null || e.Id != excludeEventId))
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Status,
                e.StartAtUtc,
                e.EndAtUtc
            })
            .ToListAsync(ct);

        return candidates
            .Where(e =>
            {
                var otherEnd = EffectiveEnd(e.StartAtUtc, e.EndAtUtc);
                return e.StartAtUtc < end && start < otherEnd;
            })
            .OrderBy(e => e.StartAtUtc)
            .Take(8)
            .Select(e => new EventConflictItemDto
            {
                Id = e.Id,
                Title = e.Title,
                Status = e.Status,
                StatusLabel = EventLabels.Status(e.Status),
                StartAtUtc = e.StartAtUtc,
                EndAtUtc = e.EndAtUtc
            })
            .ToList();
    }

    public static void EnsurePublishLocation(Event entity, bool hasSettlement = false)
    {
        var hasCoords = entity.Latitude is not null && entity.Longitude is not null;
        if (!hasCoords && entity.FacilityId is null && !hasSettlement)
            throw new ValidationException("location", "Planlanan veya yapılan kayıt için mahalle, tesis veya koordinat gerekir.");
    }

    public static EventDetailDto ToDetail(Event e, int seriesCount = 0) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        Status = e.Status,
        StatusLabel = EventLabels.Status(e.Status),
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        OrganizingUnitId = e.OrganizingUnitId,
        OrganizingUnitName = e.OrganizingUnit?.Name,
        FacilityId = e.FacilityId,
        FacilityName = e.Facility?.Name,
        Latitude = e.Latitude ?? e.Facility?.Latitude,
        Longitude = e.Longitude ?? e.Facility?.Longitude,
        Address = e.Address ?? e.Facility?.Address,
        ExpectedAttendees = e.ExpectedAttendees,
        ActualAttendees = e.ActualAttendees,
        AttendanceCount = EventAttendance.Resolve(
            e.ActualAttendees,
            (e.Settlements ?? []).Where(s => !s.IsDeleted).Sum(s => s.AttendanceCount),
            e.ExpectedAttendees),
        SeriesId = e.SeriesId,
        RecurrenceFrequency = e.RecurrenceFrequency,
        RecurrenceLabel = EventLabels.Recurrence(e.RecurrenceFrequency),
        SeriesCount = seriesCount,
        ResponsibleEmployeeId = e.ResponsibleEmployeeId,
        ResponsibleEmployeeName = e.ResponsibleEmployee is null
            ? null
            : $"{e.ResponsibleEmployee.FirstName} {e.ResponsibleEmployee.LastName}".Trim(),
        Category = e.Category,
        CategoryLabel = string.IsNullOrWhiteSpace(e.Category) ? null : EventCategories.Label(e.Category),
        Settlements = (e.Settlements ?? [])
            .Where(s => !s.IsDeleted)
            .Select(s => new EventSettlementDto
            {
                SettlementId = s.SettlementId,
                SettlementName = s.Settlement?.Name ?? "",
                OfficialCode = s.Settlement?.OfficialCode ?? "",
                AttendanceCount = s.AttendanceCount,
                UniqueBeneficiaryCount = s.UniqueBeneficiaryCount,
                Notes = s.Notes
            })
            .ToList(),
        AllowedTransitions = EventStatusTransitions.AllowedFrom(e.Status)
    };
}

internal static class EventAudit
{
    public static void Add(
        AppDbContext db,
        ICurrentUserService user,
        string action,
        Guid entityId,
        object? oldValues = null,
        object? newValues = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "Event",
            EntityId = entityId.ToString(),
            UserId = user.UserId?.ToString(),
            UserName = user.UserName ?? "system",
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues)
        });
    }
}

public sealed class GetEventsHandler : IRequestHandler<GetEventsQuery, EventListDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEventsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventListDto> Handle(GetEventsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlikleri görüntüleme yetkiniz yok.");

        var q = _db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(e => e.Title.Contains(s) || (e.Description != null && e.Description.Contains(s)));
        }

        if (request.Status is EventStatus st)
            q = q.Where(e => e.Status == st);

        if (request.FromUtc is DateTime from && request.ToUtc is DateTime to)
        {
            q = q.Where(e => e.StartAtUtc <= to && (e.EndAtUtc ?? e.StartAtUtc) >= from);
        }
        else if (request.FromUtc is DateTime fromOnly)
        {
            q = q.Where(e => (e.EndAtUtc ?? e.StartAtUtc) >= fromOnly);
        }
        else if (request.ToUtc is DateTime toOnly)
        {
            q = q.Where(e => e.StartAtUtc <= toOnly);
        }

        if (request.SettlementId is Guid sid)
            q = q.Where(e => e.Settlements.Any(x => x.SettlementId == sid));

        if (request.FacilityId is Guid fid)
        {
            var venueIds = await _db.OrganizationUnits.AsNoTracking()
                .Where(x => x.Id == fid || x.ParentId == fid)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            q = q.Where(e => e.FacilityId != null && venueIds.Contains(e.FacilityId.Value));
        }

        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (allowed is not null)
        {
            if (allowed.Count == 0)
                return new EventListDto { Items = [], TotalCount = 0 };
            q = q.Where(e => e.FacilityId != null && allowed.Contains(e.FacilityId.Value));
        }

        if (request.FromUtc is null)
        {
            var lookback = DateTime.UtcNow.AddMonths(-18);
            q = q.Where(e => (e.EndAtUtc ?? e.StartAtUtc) >= lookback);
        }

        var items = await q
            .OrderByDescending(e => e.StartAtUtc)
            .Take(2000)
            .Select(e => new EventListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                Status = e.Status,
                StatusLabel = EventLabels.Status(e.Status),
                StartAtUtc = e.StartAtUtc,
                EndAtUtc = e.EndAtUtc,
                OrganizingUnitName = e.OrganizingUnit != null ? e.OrganizingUnit.Name : null,
                FacilityId = e.FacilityId,
                FacilityName = e.Facility != null ? e.Facility.Name : null,
                Latitude = e.Latitude ?? (e.Facility != null ? e.Facility.Latitude : null),
                Longitude = e.Longitude ?? (e.Facility != null ? e.Facility.Longitude : null),
                Address = e.Address ?? (e.Facility != null ? e.Facility.Address : null),
                ExpectedAttendees = e.ExpectedAttendees,
                ActualAttendees = e.ActualAttendees,
                AttendanceCount = e.ActualAttendees != null
                    ? e.ActualAttendees
                    : (e.Settlements.Sum(s => s.AttendanceCount) > 0
                        ? e.Settlements.Sum(s => s.AttendanceCount)
                        : e.ExpectedAttendees),
                SeriesId = e.SeriesId,
                RecurrenceFrequency = e.RecurrenceFrequency,
                RecurrenceLabel = e.RecurrenceFrequency == EventRecurrenceFrequency.Weekly
                    ? "Haftalık"
                    : e.RecurrenceFrequency == EventRecurrenceFrequency.Monthly
                        ? "Aylık"
                        : null,
                ResponsibleEmployeeId = e.ResponsibleEmployeeId,
                ResponsibleEmployeeName = e.ResponsibleEmployee != null
                    ? (e.ResponsibleEmployee.FirstName + " " + e.ResponsibleEmployee.LastName).Trim()
                    : null,
                Category = e.Category
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            item.CategoryLabel = string.IsNullOrWhiteSpace(item.Category) ? null : EventCategories.Label(item.Category);

        return new EventListDto { Items = items, TotalCount = items.Count };
    }
}

public sealed class GetEventByIdHandler : IRequestHandler<GetEventByIdQuery, EventDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEventByIdHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventDetailDto?> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlikleri görüntüleme yetkiniz yok.");

        var e = await _db.Events.AsNoTracking()
            .Include(x => x.OrganizingUnit)
            .Include(x => x.Facility)
            .Include(x => x.ResponsibleEmployee)
            .Include(x => x.Settlements)
                .ThenInclude(x => x.Settlement)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e is null) return null;

        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (!UnitScopeHelper.IsUnitAllowed(allowed, e.OrganizingUnitId, e.FacilityId))
            throw new ForbiddenException("Bu etkinlik sizin tesis kapsamınızda değil.");

        var seriesCount = e.SeriesId is Guid sid
            ? await _db.Events.AsNoTracking().CountAsync(x => x.SeriesId == sid, cancellationToken)
            : 0;

        return EventConflictHelper.ToDetail(e, seriesCount);
    }
}

public sealed class CheckEventConflictsHandler : IRequestHandler<CheckEventConflictsQuery, EventConflictsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CheckEventConflictsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventConflictsDto> Handle(CheckEventConflictsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlikleri görüntüleme yetkiniz yok.");

        var items = await EventConflictHelper.FindConflictsAsync(
            _db,
            request.FacilityId,
            request.StartAtUtc,
            request.EndAtUtc,
            request.ExcludeEventId,
            cancellationToken);

        return new EventConflictsDto { HasConflicts = items.Count > 0, Items = items };
    }
}

public sealed class CreateEventHandler : IRequestHandler<CreateEventCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotificationService _notifications;
    private readonly IMemoryCache _cache;

    public CreateEventHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IUserNotificationService notifications,
        IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<Guid> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik oluşturma yetkiniz yok.");

        await EnsureRefsAsync(request.OrganizingUnitId, request.FacilityId, request.ResponsibleEmployeeId, cancellationToken);
        await UnitScopeHelper.EnsureFacilityInScopeAsync(_db, _currentUser, request.FacilityId, cancellationToken);
        var settlements = UnitScopeHelper.SeesAllUnits(_currentUser)
            ? request.Settlements
            : [];

        var start = DateTime.SpecifyKind(request.StartAtUtc, DateTimeKind.Utc);
        var end = request.EndAtUtc is null
            ? (DateTime?)null
            : DateTime.SpecifyKind(request.EndAtUtc.Value, DateTimeKind.Utc);
        var duration = end.HasValue ? end.Value - start : (TimeSpan?)null;

        var occurrences = request.RecurrenceFrequency == EventRecurrenceFrequency.None
            ? 1
            : Math.Clamp(request.RecurrenceOccurrences ?? 1, 2, 26);
        var seriesId = occurrences > 1 ? Guid.NewGuid() : (Guid?)null;

        var createdIds = new List<Guid>();
        Event? first = null;

        for (var i = 0; i < occurrences; i++)
        {
            var occStart = Shift(start, request.RecurrenceFrequency, i);
            var occEnd = duration is null ? null : occStart + duration;

            await EventConflictHelper.EnsureNoFacilityConflictsAsync(
                _db,
                request.FacilityId,
                occStart,
                occEnd,
                null,
                request.AllowConflicts,
                cancellationToken);

            var entity = new Event
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                StartAtUtc = occStart,
                EndAtUtc = occEnd,
                Status = request.Status,
                ExpectedAttendees = request.ExpectedAttendees,
                ActualAttendees = i == 0 ? request.ActualAttendees : null,
                SeriesId = seriesId,
                RecurrenceFrequency = occurrences > 1 ? request.RecurrenceFrequency : EventRecurrenceFrequency.None,
                ResponsibleEmployeeId = request.ResponsibleEmployeeId,
                OrganizingUnitId = request.OrganizingUnitId,
                FacilityId = request.FacilityId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category)
                    ? null
                    : EventCategories.Normalize(request.Category),
                CreatedBy = _currentUser.UserName ?? "system"
            };

            if (entity.Status is EventStatus.Published or EventStatus.Completed)
                EventConflictHelper.EnsurePublishLocation(entity, settlements.Count > 0);

            _db.Events.Add(entity);
            await EventSettlementSync.ApplyAsync(
                _db, entity, settlements, entity.CreatedBy ?? "system", cancellationToken);
            first ??= entity;
            createdIds.Add(entity.Id);
        }

        EventAudit.Add(_db, _currentUser, "Create", first!.Id, newValues: new
        {
            first.Title,
            first.Status,
            first.StartAtUtc,
            first.FacilityId,
            first.ExpectedAttendees,
            seriesId,
            occurrences
        });

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();

        if (first.Status == EventStatus.Published)
        {
            await _notifications.NotifyUsersWithPermissionAsync(
                PermissionCodes.EventsView,
                "Yeni etkinlik yayınlandı",
                $"{first.Title} — {first.StartAtUtc.ToLocalTime():g}",
                NotificationSeverity.Success,
                NotificationCategories.Events,
                $"/events/{first.Id}",
                cancellationToken);
        }

        return first.Id;
    }

    private static DateTime Shift(DateTime start, EventRecurrenceFrequency freq, int index)
    {
        if (index == 0 || freq == EventRecurrenceFrequency.None)
            return start;
        return freq switch
        {
            EventRecurrenceFrequency.Weekly => start.AddDays(7 * index),
            EventRecurrenceFrequency.Monthly => start.AddMonths(index),
            _ => start
        };
    }

    internal static async Task EnsureRefsAsync(
        AppDbContext db,
        Guid? organizingUnitId,
        Guid? facilityId,
        Guid? responsibleEmployeeId,
        CancellationToken ct)
    {
        if (organizingUnitId is Guid ou
            && !await db.OrganizationUnits.AnyAsync(x => x.Id == ou, ct))
            throw new NotFoundException("Düzenleyen birim bulunamadı.");

        if (facilityId is Guid fid)
        {
            var fac = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Id == fid, ct)
                ?? throw new NotFoundException("Tesis bulunamadı.");
            if (fac.Type != OrganizationUnitType.Facility)
                throw new ValidationException("facilityId", "Seçilen kayıt bir tesis değil.");
        }

        if (responsibleEmployeeId is Guid eid
            && !await db.Employees.AnyAsync(x => x.Id == eid, ct))
            throw new NotFoundException("Sorumlu personel bulunamadı.");
    }

    private Task EnsureRefsAsync(Guid? ou, Guid? fac, Guid? emp, CancellationToken ct) =>
        EnsureRefsAsync(_db, ou, fac, emp, ct);
}

public sealed class UpdateEventHandler : IRequestHandler<UpdateEventCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public UpdateEventHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik güncelleme yetkiniz yok.");

        var entity = await _db.Events.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Etkinlik bulunamadı.");

        if (!EventStatusTransitions.CanTransition(entity.Status, request.Status))
            throw new ValidationException(
                "status",
                $"{EventLabels.Status(entity.Status)} durumundan {EventLabels.Status(request.Status)} durumuna geçilemez.");

        await CreateEventHandler.EnsureRefsAsync(
            _db, request.OrganizingUnitId, request.FacilityId, request.ResponsibleEmployeeId, cancellationToken);
        await UnitScopeHelper.EnsureFacilityInScopeAsync(_db, _currentUser, request.FacilityId, cancellationToken);
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (!UnitScopeHelper.IsUnitAllowed(allowed, entity.OrganizingUnitId, entity.FacilityId))
            throw new ForbiddenException("Bu etkinlik sizin tesis kapsamınızda değil.");
        await EventConflictHelper.EnsureNoFacilityConflictsAsync(
            _db,
            request.FacilityId,
            request.StartAtUtc,
            request.EndAtUtc,
            request.Id,
            request.AllowConflicts,
            cancellationToken);

        var oldSnapshot = new
        {
            entity.Title,
            entity.Status,
            entity.StartAtUtc,
            entity.EndAtUtc,
            entity.FacilityId,
            entity.ExpectedAttendees,
            entity.ActualAttendees
        };

        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.StartAtUtc = DateTime.SpecifyKind(request.StartAtUtc, DateTimeKind.Utc);
        entity.EndAtUtc = request.EndAtUtc is null
            ? null
            : DateTime.SpecifyKind(request.EndAtUtc.Value, DateTimeKind.Utc);
        entity.Status = request.Status;
        entity.ExpectedAttendees = request.ExpectedAttendees;
        entity.ActualAttendees = request.ActualAttendees;
        entity.ResponsibleEmployeeId = request.ResponsibleEmployeeId;
        entity.OrganizingUnitId = request.OrganizingUnitId;
        entity.FacilityId = request.FacilityId;
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category)
            ? null
            : EventCategories.Normalize(request.Category);
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var directorateWide = UnitScopeHelper.SeesAllUnits(_currentUser);
        var settlementCount = directorateWide
            ? request.Settlements.Count
            : await _db.EventSettlements.CountAsync(x => x.EventId == entity.Id, cancellationToken);
        if (entity.Status is EventStatus.Published or EventStatus.Completed)
            EventConflictHelper.EnsurePublishLocation(entity, settlementCount > 0);

        if (directorateWide)
        {
            await EventSettlementSync.ApplyAsync(
                _db, entity, request.Settlements, entity.UpdatedBy ?? "system", cancellationToken);
        }

        EventAudit.Add(_db, _currentUser, "Update", entity.Id, oldSnapshot, new
        {
            entity.Title,
            entity.Status,
            entity.StartAtUtc,
            entity.EndAtUtc,
            entity.FacilityId,
            entity.ExpectedAttendees
        });

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
    }
}

public sealed class ChangeEventStatusHandler : IRequestHandler<ChangeEventStatusCommand, EventDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotificationService _notifications;
    private readonly IMemoryCache _cache;

    public ChangeEventStatusHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IUserNotificationService notifications,
        IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<EventDetailDto> Handle(ChangeEventStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik durumunu değiştirme yetkiniz yok.");

        var entity = await _db.Events
            .Include(x => x.OrganizingUnit)
            .Include(x => x.Facility)
            .Include(x => x.ResponsibleEmployee)
            .Include(x => x.Settlements)
                .ThenInclude(x => x.Settlement)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Etkinlik bulunamadı.");

        if (entity.Status == request.Status)
        {
            var count = entity.SeriesId is Guid sid
                ? await _db.Events.AsNoTracking().CountAsync(x => x.SeriesId == sid, cancellationToken)
                : 0;
            return EventConflictHelper.ToDetail(entity, count);
        }

        if (!EventStatusTransitions.CanTransition(entity.Status, request.Status))
            throw new ValidationException(
                "status",
                $"{EventLabels.Status(entity.Status)} durumundan {EventLabels.Status(request.Status)} durumuna geçilemez.");

        if (request.Status is EventStatus.Published or EventStatus.Completed)
            EventConflictHelper.EnsurePublishLocation(
                entity,
                await _db.EventSettlements.AnyAsync(x => x.EventId == entity.Id, cancellationToken));

        if (request.Status is EventStatus.Published or EventStatus.Draft)
        {
            await EventConflictHelper.EnsureNoFacilityConflictsAsync(
                _db,
                entity.FacilityId,
                entity.StartAtUtc,
                entity.EndAtUtc,
                entity.Id,
                request.AllowConflicts,
                cancellationToken);
        }

        var from = entity.Status;
        entity.Status = request.Status;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        entity.UpdatedAtUtc = DateTime.UtcNow;

        EventAudit.Add(_db, _currentUser, "ChangeStatus", entity.Id,
            new { status = from },
            new { status = entity.Status });

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();

        if (entity.Status == EventStatus.Published && from != EventStatus.Published)
        {
            await _notifications.NotifyUsersWithPermissionAsync(
                PermissionCodes.EventsView,
                "Etkinlik yayınlandı",
                $"{entity.Title} — {entity.StartAtUtc.ToLocalTime():g}",
                NotificationSeverity.Success,
                NotificationCategories.Events,
                $"/events/{entity.Id}",
                cancellationToken);
        }

        var seriesCount = entity.SeriesId is Guid seriesId
            ? await _db.Events.AsNoTracking().CountAsync(x => x.SeriesId == seriesId, cancellationToken)
            : 0;

        return EventConflictHelper.ToDetail(entity, seriesCount);
    }
}

public sealed class DeleteEventHandler : IRequestHandler<DeleteEventCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public DeleteEventHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task Handle(DeleteEventCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik silme yetkiniz yok.");

        var entity = await _db.Events.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Etkinlik bulunamadı.");

        EventAudit.Add(_db, _currentUser, "Delete", entity.Id, newValues: new
        {
            entity.Title,
            entity.Status,
            entity.StartAtUtc
        });

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
    }
}

public sealed class GetMapPinsHandler : IRequestHandler<GetMapPinsQuery, MapPinsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMapPinsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MapPinsDto> Handle(GetMapPinsQuery request, CancellationToken cancellationToken)
    {
        var canEvents = _currentUser.HasPermission(PermissionCodes.EventsView)
            || _currentUser.HasPermission(PermissionCodes.EventsManage);
        var canOrg = _currentUser.HasPermission(PermissionCodes.OrganizationView)
            || _currentUser.HasPermission(PermissionCodes.OrganizationManage);

        if (!canEvents && !canOrg)
            throw new ForbiddenException("Haritayı görüntüleme yetkiniz yok.");

        var kinds = (request.Kinds ?? "facility,event")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .ToHashSet();

        var pins = new List<MapPinDto>();

        if (kinds.Contains("facility") && canOrg)
        {
            var facilitiesQ = _db.OrganizationUnits.AsNoTracking()
                .Where(x => x.Type == OrganizationUnitType.Facility
                    && x.Latitude != null && x.Longitude != null
                    && x.Status == OrganizationUnitStatus.Active);
            var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
            if (allowed is not null)
                facilitiesQ = allowed.Count == 0
                    ? facilitiesQ.Where(_ => false)
                    : facilitiesQ.Where(x => allowed.Contains(x.Id));
            var facilities = await facilitiesQ
                .Select(x => new MapPinDto
                {
                    Id = x.Id.ToString(),
                    Kind = "facility",
                    Title = x.Name,
                    Latitude = x.Latitude!.Value,
                    Longitude = x.Longitude!.Value,
                    Subtitle = x.Address,
                    LinkPath = $"/events/facilities-locations",
                    CategoryName = x.FacilityCategory != null ? x.FacilityCategory.Name : null
                })
                .ToListAsync(cancellationToken);
            pins.AddRange(facilities);
        }

        if (kinds.Contains("event") && canEvents)
        {
            var q = _db.Events.AsNoTracking()
                .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Completed);

            if (request.FromUtc is DateTime from)
                q = q.Where(e => e.StartAtUtc >= from);
            else
                q = q.Where(e => e.StartAtUtc >= DateTime.UtcNow.AddMonths(-18));
            if (request.ToUtc is DateTime to)
                q = q.Where(e => e.StartAtUtc <= to);

            var eventAllowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
            if (eventAllowed is not null)
            {
                if (eventAllowed.Count == 0)
                    q = q.Where(_ => false);
                else
                    q = q.Where(e => e.FacilityId != null && eventAllowed.Contains(e.FacilityId.Value));
            }

            var events = await q
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.StartAtUtc,
                    e.Status,
                    Lat = e.Latitude ?? (e.Facility != null ? e.Facility.Latitude : null),
                    Lng = e.Longitude ?? (e.Facility != null ? e.Facility.Longitude : null),
                    Subtitle = e.Facility != null ? e.Facility.Name : e.Address
                })
                .Where(e => e.Lat != null && e.Lng != null)
                .ToListAsync(cancellationToken);

            pins.AddRange(events.Select(e => new MapPinDto
            {
                Id = e.Id.ToString(),
                Kind = "event",
                Title = e.Title,
                Latitude = e.Lat!.Value,
                Longitude = e.Lng!.Value,
                Subtitle = e.Subtitle,
                LinkPath = $"/events/{e.Id}",
                StartAtUtc = e.StartAtUtc,
                StatusLabel = EventLabels.Status(e.Status)
            }));
        }

        return new MapPinsDto { Pins = pins };
    }
}
