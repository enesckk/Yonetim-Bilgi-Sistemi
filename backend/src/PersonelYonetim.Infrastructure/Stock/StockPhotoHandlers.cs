using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.Files;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Stock;

public sealed class UploadStockPhotoHandler(AppDbContext db, ICurrentUserService user, IFileStorageService files)
    : IRequestHandler<UploadStockPhotoCommand>
{
    public async Task Handle(UploadStockPhotoCommand request, CancellationToken ct)
    {
        StockAccess.EnsureCustodyManage(user);
        if (!user.HasPermission(PermissionCodes.FilesUpload)) throw new ForbiddenException("Görsel yükleme yetkiniz yok.");
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            throw new ValidationException("file", "Yalnızca JPG veya PNG görsel yüklenebilir.");
        if (!await db.StockItems.AnyAsync(x => x.Id == request.ItemId, ct)) throw new NotFoundException("Malzeme bulunamadı.");
        // Bound memory use even when the incoming stream has no Length/Seek support.
        await using var content = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await request.Content.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + count > 5 * 1024 * 1024) throw new ValidationException("file", "Görsel en fazla 5 MB olabilir.");
            await content.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        content.Position = 0;
        if (!FileSignatureValidator.Matches(content, extension))
            throw new ValidationException("file", "Dosya geçerli bir JPG veya PNG görsel değil.");
        content.Position = 0;
        var saved = await files.SaveAsync(content, request.FileName, extension == ".png" ? "image/png" : "image/jpeg", $"stock-photos/{request.ItemId:N}", ct);
        string? previous;
        try
        {
            previous = await StockTransaction.RunAsync(db, async () =>
            {
                var item = await db.StockItems.FirstOrDefaultAsync(x => x.Id == request.ItemId, ct)
                    ?? throw new NotFoundException("Malzeme bulunamadı.");
                var old = item.PhotoPath;
                item.PhotoPath = saved.RelativePath;
                item.UpdatedAtUtc = DateTime.UtcNow;
                item.UpdatedBy = user.UserName;
                await db.SaveChangesAsync(ct);
                return old;
            }, ct);
        }
        catch
        {
            // A commit may have succeeded even when its response was lost. Never remove a referenced photo.
            try
            {
                db.ChangeTracker.Clear();
                if (!await db.StockItems.IgnoreQueryFilters().AnyAsync(x => x.PhotoPath == saved.RelativePath, CancellationToken.None))
                    await files.DeleteIfExistsAsync(saved.RelativePath, CancellationToken.None);
            }
            catch { /* Preserve the file when commit status cannot be established. */ }
            throw;
        }
        // The database now points to the new file. Cleanup must never turn a successful save into an error.
        try { if (previous != saved.RelativePath) await files.DeleteIfExistsAsync(previous, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* An obsolete file can be cleaned up later. */ }
    }
}

public sealed class DeleteStockPhotoHandler(AppDbContext db, ICurrentUserService user, IFileStorageService files)
    : IRequestHandler<DeleteStockPhotoCommand>
{
    public async Task Handle(DeleteStockPhotoCommand request, CancellationToken ct)
    {
        StockAccess.EnsureCustodyManage(user);
        if (!user.HasPermission(PermissionCodes.FilesUpload)) throw new ForbiddenException("Görsel kaldırma yetkiniz yok.");
        var previous = await StockTransaction.RunAsync(db, async () =>
        {
            var item = await db.StockItems.FirstOrDefaultAsync(x => x.Id == request.ItemId, ct)
                ?? throw new NotFoundException("Malzeme bulunamadı.");
            var old = item.PhotoPath;
            item.PhotoPath = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.UpdatedBy = user.UserName;
            await db.SaveChangesAsync(ct);
            return old;
        }, ct);
        try { await files.DeleteIfExistsAsync(previous, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* DB already cleared; do not report a failed removal. */ }
    }
}

public sealed class OpenStockPhotoHandler(AppDbContext db, ICurrentUserService user, IFileStorageService files)
    : IRequestHandler<OpenStockPhotoQuery, StoredFileOpenResult>
{
    public async Task<StoredFileOpenResult> Handle(OpenStockPhotoQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(user);
        var item = await db.StockItems.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ItemId, ct)
            ?? throw new NotFoundException("Malzeme bulunamadı.");
        // Archived material photos remain available through their preserved custody history.
        if (item.IsDeleted && !await db.StockCustodies.AnyAsync(x => x.StockItemId == item.Id, ct))
            throw new NotFoundException("Malzeme bulunamadı.");
        if (string.IsNullOrEmpty(item.PhotoPath)) throw new NotFoundException("Malzeme görseli bulunamadı.");
        return await files.OpenReadAsync(item.PhotoPath, ct) ?? throw new NotFoundException("Görsel bulunamadı.");
    }
}
