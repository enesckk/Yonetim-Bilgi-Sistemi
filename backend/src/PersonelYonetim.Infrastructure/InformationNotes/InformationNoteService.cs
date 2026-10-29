using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.InformationNotes;

public sealed record InformationNoteDto(Guid Id, Guid? EventId, string? EventTitle, string Title, string DateText, string Organizer, string Subject, string PreparedBy, string Body, string? FileName, DateTime CreatedAtUtc);

public sealed class InformationNoteService(AppDbContext db, ICurrentUserService user)
{
    private void Ensure(bool write = false)
    {
        if (!user.HasPermission(PermissionCodes.EventsView) || (write && !user.HasPermission(PermissionCodes.EventsManage))
            || !user.HasPermission(PermissionCodes.EmployeesViewAllUnits))
            throw new ForbiddenException("Bilgi notları için müdürlük kapsamı ve ilgili görüntüleme/düzenleme yetkisi gerekli.");
    }
    private static InformationNoteDto Dto(InformationNote n) => new(n.Id, n.EventId, n.Event?.Title, n.Title, n.DateText, n.Organizer, n.Subject, n.PreparedBy, n.Body, n.FileName, n.CreatedAtUtc);

    public async Task<IReadOnlyList<InformationNoteDto>> ListAsync(Guid? eventId, string? search, CancellationToken ct = default)
    {
        Ensure();
        var q = db.InformationNotes.AsNoTracking().Where(n => n.EventId == null || !n.Event!.IsDeleted);
        if (eventId is not null) q = q.Where(n => n.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(n => n.Title.Contains(search) || n.Subject.Contains(search) || n.PreparedBy.Contains(search));
        return await q.OrderByDescending(n => n.CreatedAtUtc).Select(n => new InformationNoteDto(n.Id, n.EventId, n.Event != null ? n.Event.Title : null, n.Title, n.DateText, n.Organizer, n.Subject, n.PreparedBy, "", n.FileName, n.CreatedAtUtc)).ToListAsync(ct);
    }
    public async Task<InformationNoteDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        Ensure();
        var row = await db.InformationNotes.Include(n => n.Event).FirstOrDefaultAsync(n => n.Id == id && (n.EventId == null || !n.Event!.IsDeleted), ct)
            ?? throw new NotFoundException("Bilgi notu bulunamadı.");
        return Dto(row);
    }
    public async Task<InformationNoteDto> SaveAsync(Guid? id, NoteInput input, byte[]? file = null, string? fileName = null, CancellationToken ct = default)
    {
        Ensure(true);
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200 || string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 30000
            || (input.DateText?.Length ?? 0) > 100 || (input.Subject?.Length ?? 0) > 500 || (input.Organizer?.Length ?? 0) > 300 || (input.PreparedBy?.Length ?? 0) > 300)
            throw new ArgumentException("Başlık (en fazla 200 karakter) ve açıklama (en fazla 30.000 karakter) gerekli. Diğer alanların uzunluğunu kontrol edin.");
        if (input.EventId is Guid eventId && !await db.Events.AnyAsync(e => e.Id == eventId, ct)) throw new ArgumentException("Seçilen proje/etkinlik bulunamadı.");
        var contentType = file is not null ? InformationNoteDocuments.ValidateFile(file, fileName ?? "") : null;
        var name = file is not null ? Path.GetFileName(fileName!.Replace('\\', '/')) : null;
        if (name?.Length > 250) throw new ArgumentException("Dosya adı en fazla 250 karakter olabilir.");
        var row = id is Guid key ? await db.InformationNotes.FirstOrDefaultAsync(n => n.Id == key, ct) ?? throw new NotFoundException("Bilgi notu bulunamadı.") : new InformationNote();
        row.Title = input.Title.Trim(); row.EventId = input.EventId; row.DateText = input.DateText?.Trim() ?? "";
        row.Subject = input.Subject?.Trim() ?? ""; row.Organizer = input.Organizer?.Trim() ?? ""; row.PreparedBy = input.PreparedBy?.Trim() ?? ""; row.Body = input.Body.Trim();
        if (file is not null) { row.FileData = file; row.FileName = name; row.ContentType = contentType; }
        if (id is null) { row.CreatedBy = user.UserName; db.InformationNotes.Add(row); } else row.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct); return await GetAsync(row.Id, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Ensure(true);
        var row = await db.InformationNotes.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException("Bilgi notu bulunamadı.");
        row.IsDeleted = true; row.DeletedAtUtc = DateTime.UtcNow; row.DeletedBy = user.UserName;
        await db.SaveChangesAsync(ct);
    }
    public async Task<(byte[] Data, string Name, string Type)> FileAsync(Guid id, CancellationToken ct = default)
    {
        await GetAsync(id, ct);
        var file = await db.InformationNotes.AsNoTracking().Where(n => n.Id == id).Select(n => new { n.FileData, n.FileName, n.ContentType }).SingleAsync(ct);
        if (file.FileData is null) throw new NotFoundException("Ekli dosya bulunamadı.");
        return (file.FileData, file.FileName!, file.ContentType!);
    }
    public async Task<byte[]> ExcelAsync(Guid id, CancellationToken ct = default)
    {
        var n = await GetAsync(id, ct);
        using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Bilgi notu");
        ws.Cell(1, 1).Value = n.Title; ws.Range(1, 1, 1, 2).Merge(); ws.Row(1).Height = 32;
        ws.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#208B5F"); ws.Range(1, 1, 1, 2).Style.Font.FontColor = XLColor.White; ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
        var pairs = new[] { ("Tarih", n.DateText), ("Proje / Etkinlik", n.EventTitle ?? ""), ("Düzenleyen Kurum", n.Organizer), ("Konu", n.Subject), ("Hazırlayan", n.PreparedBy), ("Açıklama", n.Body) };
        for (var i = 0; i < pairs.Length; i++) { ws.Cell(i + 3, 1).Value = pairs[i].Item1; ws.Cell(i + 3, 2).Value = pairs[i].Item2; ws.Cell(i + 3, 1).Style.Font.Bold = true; ws.Row(i + 3).Height = Math.Min(409, Math.Max(32, (pairs[i].Item2.Length / 95 + pairs[i].Item2.Count(c => c == '\n') + 1) * 16)); }
        ws.Column(1).Width = 25; ws.Column(2).Width = 100; ws.Range(3, 1, 8, 2).Style.Alignment.WrapText = true; ws.Range(3, 1, 8, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        ws.SheetView.FreezeRows(2); ws.PageSetup.PageOrientation = XLPageOrientation.Portrait; ws.PageSetup.FitToPages(1, 0);
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }
}
