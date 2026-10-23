using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Messages;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Messages;

internal static class MessageArchiveAccess
{
    public static void EnsureAdmin(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || !user.IsInRole(RoleCodes.SystemAdmin))
            throw new ForbiddenException("Sohbet arşivini yalnızca sistem yöneticisi görüntüleyebilir.");
    }

    public static IQueryable<DirectMessage> Thread(AppDbContext db, Guid first, Guid second) =>
        db.DirectMessages.IgnoreQueryFilters().AsNoTracking().Where(m =>
            (m.SenderUserId == first && m.RecipientUserId == second) ||
            (m.SenderUserId == second && m.RecipientUserId == first));
}

public sealed class GetAdminConversationsHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<GetAdminConversationsQuery, MessageArchivePage<AdminConversationDto>>
{
    public async Task<MessageArchivePage<AdminConversationDto>> Handle(GetAdminConversationsQuery request, CancellationToken ct)
    {
        MessageArchiveAccess.EnsureAdmin(user);
        var page = Math.Clamp(request.Page, 1, 1000000);
        const int size = 20;
        var query = db.DirectMessages.IgnoreQueryFilters().AsNoTracking();
        var term = request.Search?.Trim().ToLowerInvariant().Replace("İ", "i").Replace("ı", "i");
        if (!string.IsNullOrEmpty(term))
            query = query.Where(m => m.Sender.DisplayName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term) || m.Sender.UserName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)
                || m.Recipient.DisplayName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term) || m.Recipient.UserName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term));
        // Aggregate in SQL; only per-direction summaries are loaded, never the message archive.
        var directions = await query.GroupBy(m => new { m.SenderUserId, m.RecipientUserId })
            .Select(g => new { g.Key.SenderUserId, g.Key.RecipientUserId, Count = g.Count(), LastAt = g.Max(m => m.CreatedAtUtc) })
            .ToListAsync(ct);
        var pairs = directions.GroupBy(x => x.SenderUserId.CompareTo(x.RecipientUserId) <= 0
                ? (First: x.SenderUserId, Second: x.RecipientUserId) : (First: x.RecipientUserId, Second: x.SenderUserId))
            .Select(g => new { g.Key.First, g.Key.Second, Count = g.Sum(x => x.Count), LastAt = g.Max(x => x.LastAt) })
            .OrderByDescending(x => x.LastAt).ThenBy(x => x.First).ThenBy(x => x.Second).ToList();
        var slice = pairs.Skip((page - 1) * size).Take(size).ToList();
        var ids = slice.SelectMany(x => new[] { x.First, x.Second }).Distinct().ToList();
        var people = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new MessageUserDto { Id = u.Id, DisplayName = u.DisplayName, UserName = u.UserName })
            .ToDictionaryAsync(u => u.Id, ct);
        var result = new List<AdminConversationDto>();
        foreach (var pair in slice)
        {
            var last = await MessageArchiveAccess.Thread(db, pair.First, pair.Second)
                .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
                .Select(m => new { m.SenderUserId, m.Body, m.AttachmentFileName }).FirstOrDefaultAsync(ct);
            if (last is null) continue;
            result.Add(new(people[pair.First], people[pair.Second], pair.Count, pair.LastAt,
                last.SenderUserId, string.IsNullOrWhiteSpace(last.Body) ? last.AttachmentFileName ?? "Dosya eki" : last.Body[..Math.Min(last.Body.Length, 160)]));
        }
        return new(result, pairs.Count, page, size);
    }
}

public sealed class GetAdminConversationHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<GetAdminConversationQuery, MessageArchivePage<DirectMessageDto>>
{
    public async Task<MessageArchivePage<DirectMessageDto>> Handle(GetAdminConversationQuery request, CancellationToken ct)
    {
        MessageArchiveAccess.EnsureAdmin(user);
        var page = Math.Clamp(request.Page, 1, 1000000);
        const int size = 50;
        var query = MessageArchiveAccess.Thread(db, request.FirstUserId, request.SecondUserId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        // Reading as admin must not mark messages read for their actual recipients.
        rows.Reverse();
        return new(rows.Select(m => MessageMap.ToDto(m, Guid.Empty)).ToList(), count, page, size);
    }
}

public sealed class OpenAdminMessageAttachmentHandler(AppDbContext db, ICurrentUserService user, IFileStorageService files)
    : IRequestHandler<OpenAdminMessageAttachmentQuery, MessageAttachmentFileDto>
{
    public async Task<MessageAttachmentFileDto> Handle(OpenAdminMessageAttachmentQuery request, CancellationToken ct)
    {
        MessageArchiveAccess.EnsureAdmin(user);
        var msg = await db.DirectMessages.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Mesaj bulunamadı.");
        if (string.IsNullOrWhiteSpace(msg.AttachmentPath)) throw new NotFoundException("Bu mesajda ek yok.");
        var opened = await files.OpenReadAsync(msg.AttachmentPath, ct) ?? throw new NotFoundException("Dosya bulunamadı.");
        return new() { Stream = opened.Stream, ContentType = opened.ContentType,
            DownloadFileName = Path.GetFileName(msg.AttachmentFileName ?? opened.DownloadFileName) };
    }
}
