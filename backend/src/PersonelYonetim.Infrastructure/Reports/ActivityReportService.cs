using System.Globalization;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace PersonelYonetim.Infrastructure.Reports;

public sealed record ActivityReportInput(string Title, DateOnly FromDate, DateOnly ToDate, string DateBasis = "event", string Kind = "all");
public sealed class ActivityReportPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SourceEventId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Meta { get; set; } = "";
    public string Layout { get; set; } = "pair";
    public List<Guid> ImageIds { get; set; } = [];
    public bool ShowMonthlyChart { get; set; }
}
public sealed record ActivityReportSource(Guid Id, string Title, DateTime DateUtc, bool IsDateOnly, string Status, string Category, string Facility, string Settlements, long? Attendance, string Description, DateTime RecordedAtUtc, DateTime? EndDateUtc = null);
public sealed class ActivityReportDocument
{
    public string Organization { get; set; } = "ŞEHİTKAMİL BELEDİYESİ";
    public string Department { get; set; } = "Kültür, Sanat ve Sosyal İşler Müdürlüğü";
    public string Accent { get; set; } = "#438DA4";
    public List<ActivityReportPage> Pages { get; set; } = [];
    public List<ActivityReportSource> Sources { get; set; } = [];
    public int RecordsWithoutImages { get; set; }
}
public sealed record ActivityReportSummary(Guid Id, string Title, DateOnly FromDate, DateOnly ToDate, string DateBasis, string Kind, int Revision, int SourceCount, long SourceAttendance, DateTime CreatedAtUtc);
public sealed record ActivityReportAssetDto(Guid Id, string FileName, string ContentType, Guid? SourceMediaId);
public sealed record ActivityReportDetail(ActivityReportSummary Report, ActivityReportDocument Document, IReadOnlyList<ActivityReportAssetDto> Assets);
public sealed record ActivityReportUpdate(int Revision, string Title, ActivityReportDocument Document);

public sealed class ActivityReportService(AppDbContext db, ICurrentUserService user)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private void Ensure(bool write = false)
    {
        if (!user.HasPermission(PermissionCodes.EventsView) || !user.HasPermission(PermissionCodes.EmployeesViewAllUnits)
            || write && !user.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Faaliyet raporları için müdürlük kapsamı ve etkinlik yetkisi gerekir.");
    }
    private static ActivityReportSummary Summary(ActivityReport r) => new(r.Id, r.Title, r.FromDate, r.ToDate, r.DateBasis, r.Kind, r.Revision, r.SourceCount, r.SourceAttendance, r.CreatedAtUtc);
    private static ActivityReportDocument DocumentOf(ActivityReport r) => JsonSerializer.Deserialize<ActivityReportDocument>(r.BodyJson, Json)!;
    public async Task<IReadOnlyList<ActivityReportSummary>> ListAsync(CancellationToken ct = default)
    {
        Ensure(); return await db.ActivityReports.AsNoTracking().OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ActivityReportSummary(r.Id, r.Title, r.FromDate, r.ToDate, r.DateBasis, r.Kind, r.Revision, r.SourceCount, r.SourceAttendance, r.CreatedAtUtc)).ToListAsync(ct);
    }
    private async Task<ActivityReport> Row(Guid id, CancellationToken ct) => await db.ActivityReports.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Rapor bulunamadı.");
    public async Task<ActivityReportDetail> GetAsync(Guid id, CancellationToken ct = default)
    {
        Ensure(); var r = await Row(id, ct);
        var assets = await db.ActivityReportAssets.AsNoTracking().Where(a => a.ReportId == id)
            .Select(a => new ActivityReportAssetDto(a.Id, a.FileName, a.ContentType, a.SourceMediaId)).ToListAsync(ct);
        return new(Summary(r), DocumentOf(r), assets);
    }
    public async Task<Guid> GenerateAsync(ActivityReportInput input, CancellationToken ct = default)
    {
        Ensure(true); ValidateInput(input); var id = Guid.NewGuid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync((db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead, ct);
            if (!await db.ActivityReports.IgnoreQueryFilters().AnyAsync(r => r.Id == id, ct)) await GenerateCoreAsync(db, input, user.UserName, id, ct);
            await tx.CommitAsync(ct); return id;
        });
    }
    private static void ValidateInput(ActivityReportInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200 || input.FromDate.Year < 2017 || input.ToDate.Year > 2100
            || input.ToDate < input.FromDate || input.ToDate.DayNumber - input.FromDate.DayNumber > 5 * 366
            || input.DateBasis is not ("event" or "record") || input.Kind is not ("all" or "event" or "project"))
            throw new ValidationException("report", "Başlık, tarih aralığı (en fazla 5 yıl), tarih ölçütü ve kayıt türünü kontrol edin.");
    }
    // Immutable source snapshots: report edits never modify an event; subsequent generation creates another report.
    internal static async Task<Guid> GenerateCoreAsync(AppDbContext db, ActivityReportInput input, string? author, Guid? fixedId = null, CancellationToken ct = default)
    {
        ValidateInput(input);
        var from = new DateTimeOffset(input.FromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).UtcDateTime;
        var until = new DateTimeOffset(input.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).UtcDateTime;
        var q = db.Events.AsNoTracking().Where(e => input.DateBasis == "record" ? e.CreatedAtUtc >= from && e.CreatedAtUtc < until : e.StartAtUtc >= from && e.StartAtUtc < until);
        if (input.Kind == "project") q = q.Where(e => e.Category == EventCategories.Project);
        if (input.Kind == "event") q = q.Where(e => e.Category != EventCategories.Project);
        var events = await q.Include(e => e.Facility).Include(e => e.Settlements).ThenInclude(s => s.Settlement).OrderBy(e => e.StartAtUtc).ThenBy(e => e.Id).Take(5001).ToListAsync(ct);
        if (events.Count > 5000) throw new ValidationException("report", "Bir raporda en fazla 5.000 kayıt olabilir. Tarih aralığını daraltın.");
        if (events.Count == 0) throw new ValidationException("report", "Seçilen tarih aralığında etkinlik veya proje bulunamadı.");
        var ids = events.Select(e => e.Id).ToList();
        var notes = await db.InformationNotes.AsNoTracking().Where(n => n.EventId != null && ids.Contains(n.EventId.Value))
            .Select(n => new { n.EventId, n.Title, n.Body }).ToListAsync(ct);
        var selectedQuery = db.EventMedia.AsNoTracking().Where(m => ids.Contains(m.EventId) && m.IncludeInReport);
        if (await selectedQuery.SumAsync(m => (long)m.Size, ct) > 100 * 1024 * 1024) throw new ValidationException("report", "Seçilen rapor görsellerinin toplamı 100 MB sınırını aşıyor. Daha az görsel işaretleyin.");
        var selected = await selectedQuery.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).ToListAsync(ct);
        var report = new ActivityReport { Id = fixedId ?? Guid.NewGuid(), Title = input.Title.Trim(), FromDate = input.FromDate, ToDate = input.ToDate, DateBasis = input.DateBasis, Kind = input.Kind, SourceCount = events.Count, CreatedBy = author };
        var doc = new ActivityReportDocument();
        var assets = selected.Select(m => new ActivityReportAsset { ReportId = report.Id, SourceMediaId = m.Id, FileName = m.FileName, ContentType = m.ContentType, FileData = m.FileData.ToArray(), CreatedBy = author }).ToList();
        var assetBySource = assets.ToDictionary(a => a.SourceMediaId!.Value, a => a.Id);
        foreach (var e in events)
        {
            var actual = e.ActualAttendees is int count ? (long?)count : e.Settlements.Any(s => !s.IsDeleted && s.AttendanceCount > 0) ? e.Settlements.Where(s => !s.IsDeleted).Sum(s => (long)s.AttendanceCount) : null;
            var bodyParts = new List<string>(); if (!string.IsNullOrWhiteSpace(e.Description)) bodyParts.Add(e.Description.Trim());
            foreach (var n in notes.Where(n => n.EventId == e.Id)) if (!bodyParts.Contains(n.Body.Trim())) bodyParts.Add($"{n.Title}\n{n.Body.Trim()}");
            var body = string.Join("\n\n", bodyParts);
            var source = new ActivityReportSource(e.Id, e.Title, DateTime.SpecifyKind(e.StartAtUtc, DateTimeKind.Utc), e.IsDateOnly, EventLabels.Status(e.Status), EventCategories.Label(e.Category ?? "other", e.CategoryName), e.Facility?.Name ?? e.Address ?? "", string.Join(", ", e.Settlements.Where(s => !s.IsDeleted).Select(s => s.Settlement?.Name).Where(s => s != null)), actual, body, DateTime.SpecifyKind(e.CreatedAtUtc, DateTimeKind.Utc), e.EndAtUtc.HasValue ? DateTime.SpecifyKind(e.EndAtUtc.Value, DateTimeKind.Utc) : null);
            doc.Sources.Add(source);
            var imageIds = selected.Where(m => m.EventId == e.Id).Select(m => assetBySource[m.Id]).ToList();
            if (imageIds.Count == 0) doc.RecordsWithoutImages++;
            var meta = string.Join(" · ", new[] { Period(e), source.Status, source.Facility, source.Settlements, actual.HasValue ? $"{actual.Value.ToString("N0", Turkish)} kayıtlı katılım" : "Katılım bilgisi girilmemiş" }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var chunks = new List<string>(); var at = 0;
            do { var limit = chunks.Count * 2 < imageIds.Count ? 1100 : 2400; var part = SplitText(body[at..], limit)[0]; chunks.Add(part); at += part.Length; } while (at < body.Length);
            var pageCount = Math.Max(chunks.Count, (imageIds.Count + 1) / 2);
            for (var i = 0; i < pageCount; i++) doc.Pages.Add(new ActivityReportPage { SourceEventId = e.Id, Title = e.Title + (pageCount > 1 ? $" ({i + 1}/{pageCount})" : ""), Body = i < chunks.Count ? chunks[i] : "", Meta = meta, ImageIds = imageIds.Skip(i * 2).Take(2).ToList() });
        }
        report.SourceAttendance = doc.Sources.Sum(s => s.Attendance ?? 0);
        var summary = $"{input.FromDate:dd.MM.yyyy} – {input.ToDate:dd.MM.yyyy}\n\n{events.Count} etkinlik / proje kaydı\n{events.Count(e => e.Category == EventCategories.Project)} proje\n{report.SourceAttendance.ToString("N0", Turkish)} toplam kayıtlı katılım\n\nToplam katılım tekil kişi sayısı değildir. Beklenen katılım bu toplama dahil edilmemiştir.\n\n" + string.Join("\n", doc.Sources.GroupBy(s => s.Status).Select(g => $"{g.Key}: {g.Count()} kayıt")) + $"\n\nTarih ölçütü: {(input.DateBasis == "event" ? "Etkinlik başlangıç tarihi" : "Sisteme kayıt tarihi")}\nKaynakların alındığı tarih: {DateTime.UtcNow.AddHours(3):dd.MM.yyyy HH:mm}\nRapora seçilmiş görseli bulunmayan kayıt: {doc.RecordsWithoutImages}";
        doc.Pages.Insert(0, new ActivityReportPage { Title = input.Title.Trim(), Body = summary, Meta = "Etkinlik ve projeler · Genel değerlendirme", Layout = "text", ShowMonthlyChart = input.FromDate.Year == input.ToDate.Year });
        report.BodyJson = JsonSerializer.Serialize(doc, Json);
        if (doc.Pages.Count > 3000 || report.BodyJson.Length > 7_000_000) throw new ValidationException("report", "Rapor çok büyük. Dönemi daha küçük tarih aralıklarına ayırın.");
        db.ActivityReports.Add(report); db.ActivityReportAssets.AddRange(assets); await db.SaveChangesAsync(ct); return report.Id;
    }
    private static string Period(Event e)
    {
        var format = e.IsDateOnly ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm";
        var start = e.StartAtUtc.AddHours(3); var end = e.EndAtUtc?.AddHours(3);
        return start.ToString(format, Turkish) + (end is DateTime last && (e.IsDateOnly ? last.Date != start.Date : last != start) ? " - " + last.ToString(format, Turkish) : "");
    }
    public static List<string> SplitText(string text, int limit)
    {
        if (text.Length == 0) return [""];
        var parts = new List<string>();
        for (var at = 0; at < text.Length;)
        {
            var count = Math.Min(limit, text.Length - at);
            if (at + count < text.Length) { var boundary = text.LastIndexOf('.', at + count - 1, count); if (boundary < at + count / 2) boundary = text.LastIndexOf(' ', at + count - 1, count); if (boundary > at + count / 2) count = boundary - at + 1; }
            parts.Add(text.Substring(at, count)); at += count;
        }
        return parts;
    }
    public async Task<ActivityReportDetail> SaveAsync(Guid id, ActivityReportUpdate update, CancellationToken ct = default)
    {
        Ensure(true); var row = await Row(id, ct);
        if (row.Revision != update.Revision) throw new ConflictException("Bu rapor başka bir oturumda güncellendi. Düzenlemelerinizi kopyalayıp güncel raporu açın.");
        var d = update.Document;
        if (d is null || string.IsNullOrWhiteSpace(update.Title) || update.Title.Length > 200 || d.Organization is null || d.Department is null || d.Organization.Length > 200 || d.Department.Length > 200
            || d.Accent is null || !Regex.IsMatch(d.Accent, "^#[0-9a-fA-F]{6}$") || d.Pages is null || d.Pages.Count is < 1 or > 3000 || d.Pages.Any(p => p is null || p.Title is null || p.Title.Length > 400 || p.Body is null || p.Body.Length > 20000 || p.Meta is null || p.Meta.Length > 3000 || p.Layout is not ("single" or "pair" or "text") || p.ImageIds is null || p.ImageIds.Count > 4)
            || d.Pages.Select(p => p.Id).Distinct().Count() != d.Pages.Count)
            throw new ValidationException("report", "Rapor başlığını, sayfalarını ve tema alanlarını kontrol edin.");
        var assetIds = await db.ActivityReportAssets.Where(a => a.ReportId == id).Select(a => a.Id).ToListAsync(ct);
        if (d.Pages.SelectMany(p => p.ImageIds).Any(a => !assetIds.Contains(a))) throw new ValidationException("images", "Bu rapora ait olmayan görsel kullanılamaz.");
        var old = DocumentOf(row);
        if (d.Pages.Any(p => p.SourceEventId is Guid sourceId && !old.Sources.Any(s => s.Id == sourceId))) throw new ValidationException("source", "Sayfa kaynağı bu rapora ait olmalıdır.");
        d.Sources = old.Sources; d.RecordsWithoutImages = old.RecordsWithoutImages; // source totals are not user-editable
        row.Title = update.Title.Trim(); row.BodyJson = JsonSerializer.Serialize(d, Json); row.Revision++; row.UpdatedBy = user.UserName;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new ConflictException("Rapor başka bir oturumda değişti. Güncel raporu açın."); }
        return await GetAsync(id, ct);
    }
    public async Task<ActivityReportAssetDto> UploadAsync(Guid id, EventMediaUpload upload, CancellationToken ct = default)
    {
        Ensure(true); await Row(id, ct); var type = EventMediaService.Validate(upload);
        if (await db.ActivityReportAssets.Where(a => a.ReportId == id).SumAsync(a => (long)a.FileData.Length, ct) + upload.Data.Length > 100 * 1024 * 1024) throw new ValidationException("file", "Rapor görselleri toplam 100 MB sınırını aşıyor.");
        var asset = new ActivityReportAsset { ReportId = id, FileData = upload.Data, FileName = Path.GetFileName(upload.Name.Replace('\\', '/')), ContentType = type, CreatedBy = user.UserName };
        db.ActivityReportAssets.Add(asset); await db.SaveChangesAsync(ct); return new(asset.Id, asset.FileName, type, null);
    }
    public async Task<(byte[] Data, string Type)> AssetAsync(Guid reportId, Guid id, CancellationToken ct = default)
    {
        Ensure(); await Row(reportId, ct);
        var asset = await db.ActivityReportAssets.AsNoTracking().Where(a => a.ReportId == reportId && a.Id == id).Select(a => new { a.FileData, a.ContentType }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Rapor görseli bulunamadı.");
        return (asset.FileData, asset.ContentType);
    }
    public async Task<byte[]> ExcelAsync(Guid id, CancellationToken ct = default)
    {
        var r = await GetAsync(id, ct); using var book = new XLWorkbook(); var s = book.AddWorksheet("Etkinlik ve projeler");
        var heads = new[] { "Başlık", "Etkinlik tarihi (Türkiye)", "Tür", "Durum", "Tesis / yer", "Mahalleler", "Gerçekleşen katılım", "Açıklama / bilgi notu", "Etkinlik bitiş tarihi (Türkiye)" };
        for (var i = 0; i < heads.Length; i++) s.Cell(1, i + 1).Value = heads[i];
        var n = 2;
        foreach (var row in r.Document.Sources)
        {
            var chunks = SplitText(row.Description, 30000);
            for (var i = 0; i < chunks.Count; i++, n++)
            {
                s.Cell(n, 1).Value = row.Title + (i > 0 ? " (açıklama devamı)" : "");
                s.Cell(n, 2).Value = row.DateUtc.AddHours(3).ToString(row.IsDateOnly ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm");
                s.Cell(n, 3).Value = row.Category; s.Cell(n, 4).Value = row.Status; s.Cell(n, 5).Value = row.Facility; s.Cell(n, 6).Value = row.Settlements;
                if (i == 0 && row.Attendance.HasValue) s.Cell(n, 7).Value = row.Attendance.Value;
                s.Cell(n, 8).Value = chunks[i];
                if (row.EndDateUtc is DateTime end) s.Cell(n, 9).Value = end.AddHours(3).ToString(row.IsDateOnly ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm", Turkish);
            }
        }
        s.Range(1, 1, 1, 9).Style.Fill.BackgroundColor = XLColor.FromHtml(r.Document.Accent); s.Range(1, 1, 1, 9).Style.Font.FontColor = XLColor.White; s.Range(1, 1, 1, 9).Style.Font.Bold = true;
        s.Columns(1, 6).Width = 28; s.Column(9).Width = 28; s.Column(7).Width = 24; s.Column(8).Width = 90; s.RangeUsed()!.Style.Alignment.WrapText = true; s.SheetView.FreezeRows(1); s.RangeUsed()!.SetAutoFilter();
        using var ms = new MemoryStream(); book.SaveAs(ms); return ms.ToArray();
    }
    public async Task<byte[]> WordAsync(Guid id, CancellationToken ct = default)
    {
        var report = await GetAsync(id, ct);
        var ids = report.Document.Pages.Where(p => p.Layout != "text").SelectMany(p => p.ImageIds).Distinct().ToList();
        var assets = await db.ActivityReportAssets.AsNoTracking().Where(a => a.ReportId == id && ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return ReportWordDocument.Activity(report, assets);
    }
    public async Task<byte[]> PdfAsync(Guid id, CancellationToken ct = default)
    {
        var r = await GetAsync(id, ct);
        var usedImages = r.Document.Pages.Where(p => p.Layout != "text").SelectMany(p => p.ImageIds).Distinct().ToList();
        var assets = await db.ActivityReportAssets.AsNoTracking().Where(a => a.ReportId == id && usedImages.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        QuestPDF.Settings.License = LicenseType.Community;
        try
        {
            return QuestPDF.Fluent.Document.Create(document =>
            {
                foreach (var section in r.Document.Pages) document.Page(page =>
                {
                    page.Size(PageSizes.A4); page.Margin(32); page.DefaultTextStyle(x => x.FontSize(10).FontColor("#263641").DisableFontFeature("liga").DisableFontFeature("clig"));
                    page.Header().PaddingBottom(18).BorderBottom(1).BorderColor(r.Document.Accent).Row(row => { row.RelativeItem().Text(r.Document.Organization).FontSize(10).Bold().FontColor(r.Document.Accent); row.RelativeItem().AlignRight().Text(r.Document.Department).FontSize(8); });
                    page.Content().Column(c =>
                    {
                        c.Spacing(12); c.Item().Background(r.Document.Accent).Padding(12).Text(section.Title).FontSize(18).Bold().FontColor("#FFFFFF");
                        if (!string.IsNullOrWhiteSpace(section.Meta)) c.Item().Text(section.Meta).FontSize(9).FontColor("#607480");
                        if (section.Layout != "text" && section.ImageIds.Count > 0)
                        {
                            var ids = section.ImageIds.Where(assets.ContainsKey).ToList();
                            foreach (var chunk in ids.Chunk(section.Layout == "single" ? 1 : 2)) c.Item().Row(row => { row.Spacing(10); foreach (var image in chunk) row.RelativeItem().Height(chunk.Length == 1 ? 210 : 160).Image(assets[image].FileData).FitArea(); });
                        }
                        if (!string.IsNullOrWhiteSpace(section.Body)) c.Item().Text(section.Body).LineHeight(1.35f);
                        if (section.ShowMonthlyChart && r.Report.FromDate.Year == r.Report.ToDate.Year)
                        {
                            var values = Enumerable.Range(1, 12).Select(month => r.Document.Sources.Count(s => (r.Report.DateBasis == "record" ? s.RecordedAtUtc : s.DateUtc).AddHours(3).Year == r.Report.FromDate.Year && (r.Report.DateBasis == "record" ? s.RecordedAtUtc : s.DateUtc).AddHours(3).Month == month)).ToArray(); var max = Math.Max(1, values.Max());
                            var labels = new[] { "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
                            c.Item().PaddingTop(20).Text(r.Report.DateBasis == "record" ? "Kayıt tarihine göre aylık dağılım" : "Etkinlik tarihlerine göre aylık kayıt dağılımı").FontSize(11).Bold();
                            c.Item().Height(115).Row(row => { row.Spacing(4); for (var i = 0; i < 12; i++) { var n = i; row.RelativeItem().AlignBottom().Column(bar => { bar.Item().AlignCenter().Text(values[n].ToString()).FontSize(8); bar.Item().Height(Math.Max(2, 80f * values[n] / max)).Background(r.Document.Accent); bar.Item().AlignCenter().Text(labels[n]).FontSize(8); }); } });
                        }
                    });
                    page.Footer().PaddingTop(12).Row(row => { row.RelativeItem().Text(r.Report.Title).FontSize(8).FontColor(r.Document.Accent); row.ConstantItem(40).AlignRight().Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); }); });
                });
            }).GeneratePdf();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new ValidationException("pdf", "PDF oluşturulamadı. Görsellerin açılabildiğini ve sayfa başlıklarının uzunluğunu kontrol edin; düzenlemeleriniz kayıtta korunuyor."); }
    }
}
