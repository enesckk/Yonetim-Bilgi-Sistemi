using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Events;

public sealed class GetEventTimelineHandler : IRequestHandler<GetEventTimelineQuery, EventTimelineDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEventTimelineHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventTimelineDto> Handle(GetEventTimelineQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik geçmişini görüntüleme yetkiniz yok.");

        var exists = await _db.Events.AsNoTracking().AnyAsync(x => x.Id == request.EventId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Etkinlik bulunamadı.");

        var id = request.EventId.ToString();
        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == "Event" && x.EntityId == id)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(40)
            .Select(x => new EventTimelineItemDto
            {
                OccurredAtUtc = x.OccurredAtUtc,
                Action = x.Action,
                ActionLabel = x.Action == "Create" ? "Oluşturuldu"
                    : x.Action == "Update" ? "Güncellendi"
                    : x.Action == "ChangeStatus" ? "Durum değişti"
                    : x.Action == "Delete" ? "Silindi"
                    : x.Action,
                UserName = x.UserName,
                Detail = x.NewValuesJson
            })
            .ToListAsync(cancellationToken);

        return new EventTimelineDto { Items = logs };
    }
}

public sealed class GetEventFacilityStatsHandler : IRequestHandler<GetEventFacilityStatsQuery, EventFacilityStatsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEventFacilityStatsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventFacilityStatsDto> Handle(GetEventFacilityStatsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik istatistiklerini görüntüleme yetkiniz yok.");

        var now = DateTime.UtcNow;
        var rows = await _db.Events.AsNoTracking()
            .Where(e => e.FacilityId != null && e.Status != EventStatus.Cancelled)
            .GroupBy(e => new { e.FacilityId, Name = e.Facility!.Name })
            .Select(g => new FacilityEventStatDto
            {
                FacilityId = g.Key.FacilityId!.Value,
                FacilityName = g.Key.Name,
                TotalEvents = g.Count(),
                UpcomingEvents = g.Count(x => x.StartAtUtc >= now && (x.Status == EventStatus.Published || x.Status == EventStatus.Draft)),
                PublishedEvents = g.Count(x => x.Status == EventStatus.Published)
            })
            .OrderByDescending(x => x.UpcomingEvents)
            .ThenByDescending(x => x.TotalEvents)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new EventFacilityStatsDto { Items = rows };
    }
}

public sealed class ImportFacilityCoordsHandler : IRequestHandler<ImportFacilityCoordsCommand, FacilityCoordsImportResultDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ImportFacilityCoordsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FacilityCoordsImportResultDto> Handle(
        ImportFacilityCoordsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.OrganizationManage))
            throw new ForbiddenException("Tesis koordinatı aktarma yetkiniz yok.");

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext is not (".csv" or ".txt"))
            throw new ValidationException("file", "Yalnızca CSV dosyası desteklenir.");

        using var reader = new StreamReader(request.FileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new ValidationException("file", "CSV boş.");

        var headers = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "").Replace("_", "")).ToList();

        var codeIdx = headers.FindIndex(h => h is "tesiskodu" or "kod" or "code");
        var latIdx = headers.FindIndex(h => h is "enlem" or "latitude" or "lat");
        var lngIdx = headers.FindIndex(h => h is "boylam" or "longitude" or "lng" or "lon");
        if (codeIdx < 0 || latIdx < 0 || lngIdx < 0)
            throw new ValidationException("file", "CSV başlıkları: TesisKodu, Enlem, Boylam olmalı.");

        var facilities = await _db.OrganizationUnits
            .Where(x => x.Type == OrganizationUnitType.Facility && x.Code != null)
            .ToListAsync(cancellationToken);
        var byCode = facilities.ToDictionary(x => x.Code!, x => x, StringComparer.OrdinalIgnoreCase);

        var rows = new List<FacilityCoordsImportRowDto>();
        var rowNo = 1;
        while (!reader.EndOfStream && rowNo <= 2000)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            rowNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(',');
            string Cell(int i) => i >= 0 && i < cols.Length ? cols[i].Trim().Trim('"') : "";

            var code = Cell(codeIdx);
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(code))
                errors.Add("Tesis kodu boş.");
            else if (!byCode.ContainsKey(code))
                errors.Add($"Tesis bulunamadı: {code}");

            if (!double.TryParse(Cell(latIdx), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || lat is < -90 or > 90)
                errors.Add("Enlem geçersiz.");
            if (!double.TryParse(Cell(lngIdx), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
                || lng is < -180 or > 180)
                errors.Add("Boylam geçersiz.");

            if (errors.Count > 0 || !byCode.TryGetValue(code, out var fac))
            {
                rows.Add(new FacilityCoordsImportRowDto
                {
                    RowNumber = rowNo,
                    Success = false,
                    FacilityName = code,
                    Errors = errors
                });
                continue;
            }

            fac.Latitude = lat;
            fac.Longitude = lng;
            fac.UpdatedBy = _currentUser.UserName ?? "import";
            fac.UpdatedAtUtc = DateTime.UtcNow;
            rows.Add(new FacilityCoordsImportRowDto
            {
                RowNumber = rowNo,
                Success = true,
                FacilityId = fac.Id,
                FacilityName = fac.Name,
                Errors = []
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new FacilityCoordsImportResultDto
        {
            TotalRows = rows.Count,
            SuccessCount = rows.Count(r => r.Success),
            FailureCount = rows.Count(r => !r.Success),
            Rows = rows
        };
    }
}
