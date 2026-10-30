using System.Data;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;
namespace PersonelYonetim.Infrastructure.Events;

public sealed record EventMediaDto(Guid Id, string Kind, string FileName, string ContentType, int Size, bool IncludeInReport);
public sealed record EventMediaUpload(string Name, byte[] Data);
public sealed class EventMediaService(AppDbContext db, ICurrentUserService user)
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private async Task Ensure(Guid eventId, bool write, CancellationToken ct)
    {
        if (!user.HasPermission(PermissionCodes.EventsView) && !user.HasPermission(PermissionCodes.EventsManage)
            || write && !user.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik dosyaları için yetkiniz yok.");
        var e = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => new { e.OrganizingUnitId, e.FacilityId }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Etkinlik bulunamadı.");
        var scope = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (!UnitScopeHelper.IsUnitAllowed(scope, e.OrganizingUnitId, e.FacilityId))
            throw new ForbiddenException("Bu etkinlik sizin tesis kapsamınızda değil.");
    }
    public static string Validate(EventMediaUpload file)
    {
        var d = file.Data;
        if (d.Length == 0 || d.Length > MaxBytes) throw new ValidationException("file", "Her görsel 0–8 MB arasında olmalı.");
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (file.Name.Length > 250) throw new ValidationException("file", "Dosya adı en fazla 250 karakter olabilir.");
        if (ext == ".png" && d.Length >= 24 && d.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && d.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return "image/png";
        if (ext is ".jpg" or ".jpeg" && d.Length >= 4 && d[0] == 255 && d[1] == 216 && d[2] == 255 && d[^2] == 255 && d[^1] == 217) return "image/jpeg";
        if (ext == ".webp" && d.Length >= 30 && d.AsSpan(0, 4).SequenceEqual("RIFF"u8) && d.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        throw new ValidationException("file", "JPG, PNG veya WebP biçiminde bir görsel yükleyin. Dosya içeriği uzantısıyla eşleşmeli.");
    }
    public async Task<IReadOnlyList<EventMediaDto>> ListAsync(Guid eventId, CancellationToken ct = default)
    {
        await Ensure(eventId, false, ct);
        return await db.EventMedia.AsNoTracking().Where(x => x.EventId == eventId)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new EventMediaDto(x.Id, x.Kind, x.FileName, x.ContentType, x.Size, x.IncludeInReport)).ToListAsync(ct);
    }
    public async Task SaveAsync(Guid eventId, string kind, IReadOnlyList<EventMediaUpload> files, CancellationToken ct = default)
    {
        await Ensure(eventId, true, ct);
        if (kind is not ("poster" or "photo") || files.Count == 0 || files.Count > 20 || kind == "poster" && files.Count != 1)
            throw new ValidationException("files", "Bir afiş veya en fazla 20 etkinlik görseli seçin.");
        var types = files.Select(Validate).ToArray(); // validate all before replacing any file
        var ids = files.Select(_ => Guid.NewGuid()).ToArray();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var entry in db.ChangeTracker.Entries<EventMedia>().ToList()) entry.State = EntityState.Detached;
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (kind == "photo" && await db.EventMedia.CountAsync(x => x.EventId == eventId && x.Kind == "photo", ct) + files.Count > 20)
                throw new ValidationException("files", "Bir etkinlikte en fazla 20 fotoğraf olabilir.");
            for (var i = 0; i < files.Count; i++)
            {
                // The event ID is reserved for its poster: concurrent uploads cannot create two posters.
                var row = kind == "poster" ? await db.EventMedia.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == eventId, ct) : null;
                if (row is null) { row = new EventMedia { Id = kind == "poster" ? eventId : ids[i], EventId = eventId, Kind = kind, CreatedBy = user.UserName }; db.EventMedia.Add(row); }
                row.IncludeInReport = false; row.IsDeleted = false; row.DeletedAtUtc = null; row.DeletedBy = null;
                row.FileName = Path.GetFileName(files[i].Name.Replace('\\', '/'));
                row.ContentType = types[i]; row.FileData = files[i].Data; row.Size = files[i].Data.Length;
            }
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        });
    }
    public async Task<(byte[] Data, string Type, string Name)> ReadAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await Ensure(eventId, false, ct);
        var row = await db.EventMedia.AsNoTracking().Where(x => x.EventId == eventId && x.Id == id)
            .Select(x => new { x.FileData, x.ContentType, x.FileName }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Görsel bulunamadı.");
        return (row.FileData, row.ContentType, row.FileName);
    }
    public async Task SelectForReportAsync(Guid eventId, Guid id, bool selected, CancellationToken ct = default)
    {
        await Ensure(eventId, true, ct);
        var row = await db.EventMedia.FirstOrDefaultAsync(x => x.EventId == eventId && x.Id == id, ct) ?? throw new NotFoundException("Görsel bulunamadı.");
        row.IncludeInReport = selected; await db.SaveChangesAsync(ct);
    }
    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await Ensure(eventId, true, ct);
        var row = await db.EventMedia.FirstOrDefaultAsync(x => x.EventId == eventId && x.Id == id, ct)
            ?? throw new NotFoundException("Görsel bulunamadı.");
        row.IsDeleted = true; row.DeletedAtUtc = DateTime.UtcNow; row.DeletedBy = user.UserName;
        row.FileData = []; row.Size = 0;
        await db.SaveChangesAsync(ct);
    }
}
