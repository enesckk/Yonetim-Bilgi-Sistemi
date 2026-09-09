using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Events;

public sealed class GetEventNotesHandler
    : IRequestHandler<GetEventNotesQuery, IReadOnlyList<EventNoteDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEventNotesHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EventNoteDto>> Handle(
        GetEventNotesQuery request,
        CancellationToken cancellationToken)
    {
        EnsureCanView();

        var exists = await _db.Events.AsNoTracking()
            .AnyAsync(e => e.Id == request.EventId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Etkinlik bulunamadı.");

        return await _db.EventNotes.AsNoTracking()
            .Where(n => n.EventId == request.EventId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new EventNoteDto
            {
                Id = n.Id,
                AuthorUserId = n.AuthorUserId,
                AuthorName = n.Author.DisplayName,
                Body = n.Body,
                CreatedAtUtc = n.CreatedAtUtc,
                SharedRecipientCount = 0
            })
            .ToListAsync(cancellationToken);
    }

    private void EnsureCanView()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Etkinlik notlarını görüntüleme yetkiniz yok.");
    }
}

public sealed class CreateEventNoteHandler : IRequestHandler<CreateEventNoteCommand, EventNoteDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateEventNoteHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EventNoteDto> Handle(
        CreateEventNoteCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Not ekleme yetkiniz yok.");

        var me = _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken)
            ?? throw new NotFoundException("Etkinlik bulunamadı.");

        var wantsShare = request.ShareToDirectors
            || request.ShareToUnitManagers
            || (request.RecipientUserIds?.Count > 0);
        if (wantsShare
            && !_currentUser.HasPermission(PermissionCodes.MessagesUse)
            && !_currentUser.HasPermission(PermissionCodes.NotificationsView))
        {
            throw new ForbiddenException("Notu iletmek için mesajlaşma yetkiniz yok.");
        }

        var body = request.Body.Trim();
        var note = new EventNote
        {
            EventId = ev.Id,
            AuthorUserId = me,
            Body = body,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        _db.EventNotes.Add(note);

        var recipients = wantsShare
            ? await ResolveRecipientsAsync(me, request, cancellationToken)
            : [];

        if (recipients.Count > 0)
        {
            var preview = body.Length > 600 ? body[..600] + "…" : body;
            var dmBody = $"Etkinlik notu: {ev.Title}\n\n{preview}";
            foreach (var recipientId in recipients)
            {
                _db.DirectMessages.Add(new DirectMessage
                {
                    SenderUserId = me,
                    RecipientUserId = recipientId,
                    Body = dmBody,
                    RelatedEventId = ev.Id,
                    CreatedBy = _currentUser.UserName ?? "system"
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var authorName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == me)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? _currentUser.UserName ?? "Kullanıcı";

        return new EventNoteDto
        {
            Id = note.Id,
            AuthorUserId = me,
            AuthorName = authorName,
            Body = note.Body,
            CreatedAtUtc = note.CreatedAtUtc,
            SharedRecipientCount = recipients.Count
        };
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(
        Guid me,
        CreateEventNoteCommand request,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>(request.RecipientUserIds ?? []);
        var roleCodes = new List<string>();
        if (request.ShareToDirectors)
        {
            roleCodes.Add(RoleCodes.Director);
            roleCodes.Add(RoleCodes.DeputyDirector);
        }

        if (request.ShareToUnitManagers)
            roleCodes.Add(RoleCodes.UnitManager);

        if (roleCodes.Count > 0)
        {
            var fromRoles = await _db.UserRoles.AsNoTracking()
                .Where(ur => ur.User.IsActive && roleCodes.Contains(ur.Role.Code!))
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken);
            foreach (var id in fromRoles)
                ids.Add(id);
        }

        ids.Remove(me);
        if (ids.Count == 0)
            return [];

        return await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
