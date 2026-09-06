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

public sealed class ImportEventsHandler : IRequestHandler<ImportEventsCommand, EventImportResultDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ImportEventsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventImportResultDto> Handle(ImportEventsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik içe aktarma yetkiniz yok.");

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext is not (".csv" or ".txt"))
            throw new ValidationException("file", "Yalnızca CSV dosyası desteklenir.");

        using var reader = new StreamReader(request.FileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new ValidationException("file", "CSV boş.");

        var headers = SplitCsv(headerLine).Select(NormalizeHeader).ToList();
        var idx = BuildIndex(headers);

        Require(idx, "baslik");
        Require(idx, "baslangic");

        var facilityByCode = await _db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility && x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rows = new List<EventImportRowResultDto>();
        var rowNo = 1;
        while (!reader.EndOfStream && rowNo <= 500)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            rowNo++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = SplitCsv(line);
            string Cell(string key) =>
                idx.TryGetValue(key, out var i) && i < cols.Count ? cols[i].Trim() : "";

            var errors = new List<string>();
            var title = Cell("baslik");
            if (string.IsNullOrWhiteSpace(title))
                errors.Add("Başlık zorunlu.");

            if (!TryParseDate(Cell("baslangic"), out var start))
                errors.Add("Başlangıç tarihi geçersiz.");

            DateTime? end = null;
            var endRaw = Cell("bitis");
            if (!string.IsNullOrWhiteSpace(endRaw))
            {
                if (!TryParseDate(endRaw, out var endVal))
                    errors.Add("Bitiş tarihi geçersiz.");
                else
                    end = endVal;
            }

            var status = ParseStatus(Cell("durum"));
            Guid? facilityId = null;
            var tesisKodu = Cell("tesiskodu");
            if (!string.IsNullOrWhiteSpace(tesisKodu))
            {
                if (!facilityByCode.TryGetValue(tesisKodu, out var fac))
                    errors.Add($"Tesis kodu bulunamadı: {tesisKodu}");
                else
                    facilityId = fac.Id;
            }

            double? lat = null, lng = null;
            if (!string.IsNullOrWhiteSpace(Cell("enlem")))
            {
                if (!double.TryParse(Cell("enlem"), NumberStyles.Float, CultureInfo.InvariantCulture, out var la))
                    errors.Add("Enlem geçersiz.");
                else lat = la;
            }
            if (!string.IsNullOrWhiteSpace(Cell("boylam")))
            {
                if (!double.TryParse(Cell("boylam"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo))
                    errors.Add("Boylam geçersiz.");
                else lng = lo;
            }

            int? attendees = null;
            if (!string.IsNullOrWhiteSpace(Cell("beklenenkatilimci")))
            {
                if (!int.TryParse(Cell("beklenenkatilimci"), out var a) || a < 1)
                    errors.Add("Beklenen katılımcı geçersiz.");
                else attendees = a;
            }

            if (errors.Count > 0)
            {
                rows.Add(new EventImportRowResultDto
                {
                    RowNumber = rowNo,
                    Success = false,
                    Title = title,
                    Errors = errors
                });
                continue;
            }

            var entity = new Event
            {
                Title = title.Trim(),
                Description = NullIfEmpty(Cell("aciklama")),
                StartAtUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                EndAtUtc = end is null ? null : DateTime.SpecifyKind(end.Value, DateTimeKind.Utc),
                Status = status,
                FacilityId = facilityId,
                Latitude = lat,
                Longitude = lng,
                Address = NullIfEmpty(Cell("adres")),
                ExpectedAttendees = attendees,
                CreatedBy = _currentUser.UserName ?? "import"
            };

            if (entity.Status is EventStatus.Published or EventStatus.Completed
                && entity.FacilityId is null
                && (entity.Latitude is null || entity.Longitude is null))
            {
                rows.Add(new EventImportRowResultDto
                {
                    RowNumber = rowNo,
                    Success = false,
                    Title = title,
                    Errors = ["Yayın için tesis veya koordinat gerekir."]
                });
                continue;
            }

            _db.Events.Add(entity);
            EventAudit.Add(_db, _currentUser, "Create", entity.Id, newValues: new { import = true, entity.Title });
            rows.Add(new EventImportRowResultDto
            {
                RowNumber = rowNo,
                Success = true,
                EventId = entity.Id,
                Title = entity.Title,
                Errors = []
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new EventImportResultDto
        {
            TotalRows = rows.Count,
            SuccessCount = rows.Count(r => r.Success),
            FailureCount = rows.Count(r => !r.Success),
            Rows = rows
        };
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static EventStatus ParseStatus(string raw)
    {
        var s = raw.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (string.IsNullOrEmpty(s) || s is "taslak" or "1") return EventStatus.Draft;
        if (s is "yayinda" or "yayında" or "2") return EventStatus.Published;
        if (s is "iptal" or "3") return EventStatus.Cancelled;
        if (s is "tamamlandi" or "tamamlandı" or "4") return EventStatus.Completed;
        return EventStatus.Draft;
    }

    private static bool TryParseDate(string raw, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
            "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"
        };
        return DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value)
            || DateTime.TryParse(raw.Trim(), CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AssumeUniversal, out value);
    }

    private static void Require(Dictionary<string, int> idx, string key)
    {
        if (!idx.ContainsKey(key))
            throw new ValidationException("file", $"CSV başlığında «{key}» kolonu gerekli.");
    }

    private static Dictionary<string, int> BuildIndex(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(headers[i]))
                map[headers[i]] = i;
        }
        return map;
    }

    private static string NormalizeHeader(string h) =>
        h.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "").Replace("_", "");

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
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
