using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Events;

public sealed class GetEventImportTemplateHandler
    : IRequestHandler<GetEventImportTemplateQuery, EventImportTemplateFile>
{
    public Task<EventImportTemplateFile> Handle(
        GetEventImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Etkinlikler");
        var headers = new[]
        {
            "Baslik", "Aciklama", "Baslangic", "Bitis", "Durum",
            "Mahalle", "Kategori", "Katilim", "TesisKodu", "Adres"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "Örnek sağlık taraması";
        ws.Cell(2, 2).Value = "Mahalle taraması";
        ws.Cell(2, 3).Value = "2026-03-12 10:00";
        ws.Cell(2, 4).Value = "2026-03-12 12:00";
        ws.Cell(2, 5).Value = "Yapıldı";
        ws.Cell(2, 6).Value = "Güvenevler";
        ws.Cell(2, 7).Value = "Sağlık";
        ws.Cell(2, 8).Value = 80;
        ws.Cell(2, 10).Value = "Güvenevler Mahallesi";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(new EventImportTemplateFile
        {
            Content = ms.ToArray(),
            FileName = $"etkinlik-import-sablon-{DateTime.UtcNow:yyyyMMdd}.xlsx"
        });
    }
}

public sealed class ImportEventsHandler : IRequestHandler<ImportEventsCommand, EventImportResultDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public ImportEventsHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<EventImportResultDto> Handle(ImportEventsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik içe aktarma yetkiniz yok.");

        await using var buffer = new MemoryStream();
        await request.FileStream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
            throw new ValidationException("file", "Dosya boş.");
        if (buffer.Length > 10 * 1024 * 1024)
            throw new ValidationException("file", "Dosya en fazla 10 MB olabilir.");
        buffer.Position = 0;

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        IReadOnlyList<IReadOnlyDictionary<string, string>> table = ext switch
        {
            ".xlsx" => ReadXlsx(buffer),
            ".csv" or ".txt" => ReadCsv(buffer),
            _ => throw new ValidationException("file", "Yalnızca .xlsx veya .csv yükleyin.")
        };

        if (table.Count == 0)
            throw new ValidationException("file", "Dosyada satır yok.");
        if (!table[0].ContainsKey("baslik") || !table[0].ContainsKey("baslangic"))
            throw new ValidationException("file", "Başlık ve Başlangıç sütunları zorunlu.");

        var facilityByCode = await _db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility && x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var settlements = await _db.Settlements.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Name, s.DisplayName })
            .ToListAsync(cancellationToken);
        var settlementByFold = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var s in settlements)
        {
            settlementByFold.TryAdd(FoldName(s.Name), s.Id);
            if (!string.IsNullOrWhiteSpace(s.DisplayName))
                settlementByFold.TryAdd(FoldName(s.DisplayName), s.Id);
        }

        var actor = _currentUser.UserName ?? "import";
        var rows = new List<EventImportRowResultDto>();
        var take = Math.Min(table.Count, 500);
        for (var i = 0; i < take; i++)
        {
            var row = table[i];
            string Cell(string key) => row.TryGetValue(key, out var v) ? v : "";

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

            var status = ParseStatus(Cell("durum"), start);
            Guid? facilityId = null;
            var tesisKodu = Cell("tesiskodu");
            if (!string.IsNullOrWhiteSpace(tesisKodu))
            {
                if (!facilityByCode.TryGetValue(tesisKodu, out var facId))
                    errors.Add($"Tesis kodu bulunamadı: {tesisKodu}");
                else
                    facilityId = facId;
            }

            Guid? settlementId = null;
            var mahalle = Cell("mahalle");
            if (!string.IsNullOrWhiteSpace(mahalle))
            {
                if (!settlementByFold.TryGetValue(FoldName(mahalle), out var sid))
                    errors.Add($"Mahalle bulunamadı: {mahalle}");
                else
                    settlementId = sid;
            }

            int? attendees = ParseAttendees(Cell("katilim"), Cell("beklenenkatilimci"), errors);
            var category = ParseCategory(Cell("kategori"));

            if (errors.Count > 0)
            {
                rows.Add(Fail(i + 2, title, errors));
                continue;
            }

            if (status is EventStatus.Published or EventStatus.Completed
                && facilityId is null
                && settlementId is null)
            {
                rows.Add(Fail(i + 2, title, ["Planlanan/yapılan kayıt için mahalle veya tesis yazın."]));
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
                Address = NullIfEmpty(Cell("adres")),
                ExpectedAttendees = attendees,
                Category = category,
                CreatedBy = actor
            };
            _db.Events.Add(entity);

            if (settlementId is Guid sidOk)
            {
                _db.EventSettlements.Add(new EventSettlement
                {
                    EventId = entity.Id,
                    SettlementId = sidOk,
                    AttendanceCount = attendees ?? 0,
                    UniqueBeneficiaryCount = attendees,
                    CreatedBy = actor
                });
            }

            EventAudit.Add(_db, _currentUser, "Create", entity.Id, newValues: new { import = true, entity.Title });
            rows.Add(new EventImportRowResultDto
            {
                RowNumber = i + 2,
                Success = true,
                EventId = entity.Id,
                Title = entity.Title,
                Errors = []
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
        return new EventImportResultDto
        {
            TotalRows = rows.Count,
            SuccessCount = rows.Count(r => r.Success),
            FailureCount = rows.Count(r => !r.Success),
            Rows = rows
        };
    }

    private static EventImportRowResultDto Fail(int row, string title, List<string> errors) => new()
    {
        RowNumber = row,
        Success = false,
        Title = title,
        Errors = errors
    };

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static EventStatus ParseStatus(string raw, DateTime start)
    {
        var s = raw.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (s is "taslak" or "1") return EventStatus.Draft;
        if (s is "planlandi" or "planlandı" or "yayinda" or "yayında" or "2") return EventStatus.Published;
        if (s is "iptal" or "3") return EventStatus.Cancelled;
        if (s is "yapildi" or "yapıldı" or "tamamlandi" or "tamamlandı" or "4") return EventStatus.Completed;
        return start.Date < DateTime.UtcNow.Date ? EventStatus.Completed : EventStatus.Published;
    }

    private static string? ParseCategory(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        foreach (var (code, label) in EventCategories.All)
        {
            if (code == t || label.ToLower(CultureInfo.GetCultureInfo("tr-TR")) == t)
                return code;
        }
        return EventCategories.Normalize(raw);
    }

    private static int? ParseAttendees(string katilim, string expected, List<string> errors)
    {
        var raw = string.IsNullOrWhiteSpace(katilim) ? expected : katilim;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) &&
            !int.TryParse(raw, NumberStyles.Integer, CultureInfo.GetCultureInfo("tr-TR"), out a))
        {
            errors.Add("Katılım sayısı geçersiz.");
            return null;
        }
        if (a < 0)
        {
            errors.Add("Katılım sayısı geçersiz.");
            return null;
        }
        return a;
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

    private static string FoldName(string name)
    {
        var s = name.ToUpper(new CultureInfo("tr-TR"));
        s = s.Replace("Ç", "C").Replace("Ğ", "G").Replace("İ", "I")
            .Replace("Ö", "O").Replace("Ş", "S").Replace("Ü", "U").Replace("Â", "A");
        s = Regex.Replace(s, @"MAHALLESI|MAHALLE|KOYU|KOY", string.Empty, RegexOptions.CultureInvariant);
        return Regex.Replace(s, @"[^A-Z0-9]", string.Empty);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var firstRow = ws.FirstRowUsed() ?? throw new ValidationException("file", "Excel boş.");
        var lastRow = ws.LastRowUsed() ?? firstRow;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        var headers = new List<string>();
        for (var c = 1; c <= lastCol; c++)
            headers.Add(NormalizeHeader(firstRow.Cell(c).GetString()));

        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var r = firstRow.RowNumber() + 1; r <= lastRow.RowNumber(); r++)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var empty = true;
            for (var c = 1; c <= lastCol; c++)
            {
                var key = headers[c - 1];
                if (string.IsNullOrWhiteSpace(key)) continue;
                var text = CellText(ws.Cell(r, c));
                if (!string.IsNullOrWhiteSpace(text)) empty = false;
                map[key] = text;
            }
            if (!empty) rows.Add(map);
        }
        return rows;
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
        return cell.GetString().Trim();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new ValidationException("file", "CSV boş.");
        var headers = SplitCsv(headerLine).Select(NormalizeHeader).ToList();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = SplitCsv(line);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(headers[i])) continue;
                map[headers[i]] = i < cols.Count ? cols[i].Trim() : "";
            }
            rows.Add(map);
        }
        return rows;
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
