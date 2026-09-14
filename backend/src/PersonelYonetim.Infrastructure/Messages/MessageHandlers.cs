using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Messages;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Messages;

public sealed class GetMessageDirectoryHandler
    : IRequestHandler<GetMessageDirectoryQuery, IReadOnlyList<MessageUserDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMessageDirectoryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MessageUserDto>> Handle(
        GetMessageDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        EnsureCanMessage();
        var me = RequireUserId();
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != me);
        var term = request.Search?.Trim().ToLowerInvariant() ?? string.Empty;
        if (term.Length > 0)
        {
            q = q.Where(u => u.UserName.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }

        return await q
            .OrderByDescending(u => term.Length > 0 && u.DisplayName.ToLower().StartsWith(term))
            .ThenBy(u => u.DisplayName)
            .Take(40)
            .Select(u => new MessageUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                DisplayName = u.DisplayName
            })
            .ToListAsync(cancellationToken);
    }

    private void EnsureCanMessage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Mesajlaşma yetkiniz yok.");
    }

    private Guid RequireUserId() =>
        _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
}

public sealed class GetConversationsHandler
    : IRequestHandler<GetConversationsQuery, IReadOnlyList<ConversationDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetConversationsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ConversationDto>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Mesajlaşma yetkiniz yok.");

        var me = _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
        var mine = await _db.DirectMessages.AsNoTracking()
            .Where(m => m.SenderUserId == me || m.RecipientUserId == me)
            .Select(m => new
            {
                m.Id,
                m.SenderUserId,
                m.RecipientUserId,
                m.Body,
                m.CreatedAtUtc,
                m.ReadAtUtc,
                m.AttachmentFileName,
                OtherId = m.SenderUserId == me ? m.RecipientUserId : m.SenderUserId
            })
            .ToListAsync(cancellationToken);

        var otherIds = mine.Select(x => x.OtherId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return mine
            .GroupBy(x => x.OtherId)
            .Select(g =>
            {
                var last = g.OrderByDescending(x => x.CreatedAtUtc).First();
                users.TryGetValue(g.Key, out var other);
                var preview = string.IsNullOrWhiteSpace(last.Body)
                    ? last.AttachmentFileName ?? "Dosya eki"
                    : last.Body;
                return new ConversationDto
                {
                    OtherUserId = g.Key,
                    OtherUserName = other?.UserName ?? "",
                    OtherDisplayName = other?.DisplayName ?? other?.UserName ?? "Kullanıcı",
                    LastBody = preview,
                    LastAtUtc = last.CreatedAtUtc,
                    UnreadCount = g.Count(x => x.RecipientUserId == me && x.ReadAtUtc == null)
                };
            })
            .OrderByDescending(x => x.LastAtUtc)
            .ToList();
    }
}

public sealed class GetConversationMessagesHandler
    : IRequestHandler<GetConversationMessagesQuery, IReadOnlyList<DirectMessageDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetConversationMessagesHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DirectMessageDto>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Mesajlaşma yetkiniz yok.");

        var me = _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
        var rows = await _db.DirectMessages
            .Where(m =>
                (m.SenderUserId == me && m.RecipientUserId == request.OtherUserId)
                || (m.SenderUserId == request.OtherUserId && m.RecipientUserId == me))
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var unread = rows.Where(m => m.RecipientUserId == me && m.ReadAtUtc == null).ToList();
        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var m in unread)
                m.ReadAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return rows.Select(m => MessageMap.ToDto(m, me)).ToList();
    }
}

public sealed class GetUnreadMessageCountHandler : IRequestHandler<GetUnreadMessageCountQuery, int>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadMessageCountHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(GetUnreadMessageCountQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            return 0;

        var me = _currentUser.UserId;
        if (me is null) return 0;

        return await _db.DirectMessages.AsNoTracking()
            .CountAsync(m => m.RecipientUserId == me && m.ReadAtUtc == null, cancellationToken);
    }
}

public sealed class SendDirectMessageHandler : IRequestHandler<SendDirectMessageCommand, DirectMessageDto>
{
    private static readonly HashSet<string> MessageFileExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg" };

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public SendDirectMessageHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task<DirectMessageDto> Handle(
        SendDirectMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Mesajlaşma yetkiniz yok.");

        var me = _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
        if (request.RecipientUserId == me)
            throw new ValidationException("recipientUserId", "Kendinize mesaj gönderemezsiniz.");

        var recipient = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.RecipientUserId && u.IsActive, cancellationToken)
            ?? throw new NotFoundException("Alıcı bulunamadı.");

        string? path = null;
        string? fileName = null;
        string? contentType = null;
        if (request.Attachment is not null)
        {
            var original = request.AttachmentFileName ?? "ek";
            var ext = Path.GetExtension(original);
            if (string.IsNullOrWhiteSpace(ext) || !MessageFileExtensions.Contains(ext))
            {
                throw new ValidationException("file", "Yalnızca PDF, PNG veya JPG ekleyebilirsiniz.");
            }

            var saved = await _files.SaveAsync(
                request.Attachment,
                original,
                request.AttachmentContentType ?? "application/octet-stream",
                $"messages/{me:N}",
                cancellationToken);
            path = saved.RelativePath;
            fileName = Path.GetFileName(original);
            if (fileName.Length > 255) fileName = fileName[..255];
            contentType = string.IsNullOrWhiteSpace(request.AttachmentContentType)
                ? ContentTypeFor(ext)
                : request.AttachmentContentType.Trim();
        }

        var body = request.Body?.Trim() ?? "";
        var msg = new DirectMessage
        {
            SenderUserId = me,
            RecipientUserId = recipient.Id,
            Body = body,
            AttachmentPath = path,
            AttachmentFileName = fileName,
            AttachmentContentType = contentType,
            RelatedEventId = request.RelatedEventId,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        _db.DirectMessages.Add(msg);
        await _db.SaveChangesAsync(cancellationToken);

        return MessageMap.ToDto(msg, me);
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}

public sealed class OpenMessageAttachmentHandler
    : IRequestHandler<OpenMessageAttachmentQuery, MessageAttachmentFileDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public OpenMessageAttachmentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task<MessageAttachmentFileDto> Handle(
        OpenMessageAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Mesajlaşma yetkiniz yok.");

        var me = _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
        var msg = await _db.DirectMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj bulunamadı.");

        if (msg.SenderUserId != me && msg.RecipientUserId != me)
            throw new ForbiddenException("Bu eke erişim yetkiniz yok.");

        if (string.IsNullOrWhiteSpace(msg.AttachmentPath))
            throw new NotFoundException("Bu mesajda ek yok.");

        var opened = await _files.OpenReadAsync(msg.AttachmentPath, cancellationToken)
            ?? throw new NotFoundException("Dosya bulunamadı.");

        var download = string.IsNullOrWhiteSpace(msg.AttachmentFileName)
            ? opened.DownloadFileName
            : Path.GetFileName(msg.AttachmentFileName);

        return new MessageAttachmentFileDto
        {
            Stream = opened.Stream,
            ContentType = string.IsNullOrWhiteSpace(msg.AttachmentContentType)
                ? opened.ContentType
                : msg.AttachmentContentType,
            DownloadFileName = download
        };
    }
}

internal static class MessageMap
{
    public static DirectMessageDto ToDto(DirectMessage m, Guid me) => new()
    {
        Id = m.Id,
        SenderUserId = m.SenderUserId,
        RecipientUserId = m.RecipientUserId,
        Body = m.Body,
        SentAtUtc = m.CreatedAtUtc,
        ReadAtUtc = m.ReadAtUtc,
        Mine = m.SenderUserId == me,
        HasAttachment = !string.IsNullOrWhiteSpace(m.AttachmentPath),
        AttachmentFileName = m.AttachmentFileName,
        AttachmentContentType = m.AttachmentContentType,
        RelatedEventId = m.RelatedEventId
    };
}
