using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.WorkTasks;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.WorkTasks;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.WorkTasks;

internal static class WorkTaskAccess
{
    public static bool CanView(ICurrentUserService user) =>
        user.HasPermission(PermissionCodes.TasksView)
        || user.HasPermission(PermissionCodes.TasksAssign)
        || user.HasPermission(PermissionCodes.TasksReview)
        || user.HasPermission(PermissionCodes.TasksSubmit);

    public static bool CanAssign(ICurrentUserService user) =>
        user.HasPermission(PermissionCodes.TasksAssign);

    public static bool CanReview(ICurrentUserService user) =>
        user.HasPermission(PermissionCodes.TasksReview);

    public static bool CanSubmit(ICurrentUserService user) =>
        user.HasPermission(PermissionCodes.TasksSubmit) || CanAssign(user);

    public static Guid RequireUser(ICurrentUserService user) =>
        user.UserId ?? throw new ForbiddenException("Oturum gerekli.");
}

public sealed class GetWorkTaskOptionsHandler
    : IRequestHandler<GetWorkTaskOptionsQuery, IReadOnlyList<WorkTaskUserDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public GetWorkTaskOptionsHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<WorkTaskUserDto>> Handle(
        GetWorkTaskOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanView(_current))
            throw new ForbiddenException("İş ataması için yetkiniz yok.");

        var me = WorkTaskAccess.RequireUser(_current);
        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Id != me)
            .OrderBy(u => u.DisplayName)
            .Select(u => new WorkTaskUserDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                RoleLabel = u.UserRoles
                    .Select(r => r.Role.Name)
                    .FirstOrDefault() ?? ""
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetWorkTasksHandler
    : IRequestHandler<GetWorkTasksQuery, IReadOnlyList<WorkTaskListRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public GetWorkTasksHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<WorkTaskListRowDto>> Handle(
        GetWorkTasksQuery request,
        CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanView(_current))
            throw new ForbiddenException("İş ataması için yetkiniz yok.");

        var me = WorkTaskAccess.RequireUser(_current);
        var canReview = WorkTaskAccess.CanReview(_current);
        var q = _db.WorkTasks.AsNoTracking()
            .Where(t => t.CreatedByUserId == me || t.AssigneeUserId == me || t.ReviewerUserId == me || canReview);

        var tab = (request.Tab ?? "inbox").Trim().ToLowerInvariant();
        q = tab switch
        {
            "assigned" => q.Where(t => t.CreatedByUserId == me && t.Kind == WorkTaskKind.Assignment),
            "approval" => q.Where(t => t.Status == WorkTaskStatus.WaitingApproval || t.Status == WorkTaskStatus.RevisionRequested),
            "done" => q.Where(t =>
                t.Status == WorkTaskStatus.Approved
                || t.Status == WorkTaskStatus.Rejected
                || t.Status == WorkTaskStatus.Done
                || t.Status == WorkTaskStatus.Cancelled),
            _ => q.Where(t =>
                t.Status != WorkTaskStatus.Approved
                && t.Status != WorkTaskStatus.Rejected
                && t.Status != WorkTaskStatus.Done
                && t.Status != WorkTaskStatus.Cancelled)
        };

        var rows = await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(120)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Kind,
                t.Status,
                CreatedByName = t.CreatedByUser.DisplayName,
                AssigneeName = t.AssigneeUser != null ? t.AssigneeUser.DisplayName : null,
                UnitName = t.Unit != null ? t.Unit.Name : null,
                t.DueOn,
                t.CreatedAtUtc,
                AttachmentCount = t.Attachments.Count(a => !a.IsDeleted),
                t.CreatedByUserId,
                t.AssigneeUserId,
                t.ReviewerUserId
            })
            .ToListAsync(cancellationToken);

        return rows.Select(t => new WorkTaskListRowDto
        {
            Id = t.Id,
            Title = t.Title,
            Kind = t.Kind,
            KindLabel = WorkTaskLabels.Kind(t.Kind),
            Status = t.Status,
            StatusLabel = WorkTaskLabels.Status(t.Status),
            CreatedByName = t.CreatedByName,
            AssigneeName = t.AssigneeName,
            UnitName = t.UnitName,
            DueOn = t.DueOn,
            CreatedAtUtc = t.CreatedAtUtc,
            AttachmentCount = t.AttachmentCount,
            CanReview = canReview && t.Status is WorkTaskStatus.WaitingApproval or WorkTaskStatus.RevisionRequested,
            CanSubmit = t.AssigneeUserId == me
                && t.Status is WorkTaskStatus.Open or WorkTaskStatus.InProgress or WorkTaskStatus.RevisionRequested,
            CanCancel = (t.CreatedByUserId == me || canReview)
                && WorkTaskLifecycle.CanCancel(t.Status),
            CanDelete = (t.CreatedByUserId == me || canReview)
                && WorkTaskLifecycle.CanDelete(t.Status)
        }).ToList();
    }
}

public sealed class GetWorkTaskHandler : IRequestHandler<GetWorkTaskQuery, WorkTaskDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public GetWorkTaskHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<WorkTaskDetailDto> Handle(GetWorkTaskQuery request, CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanView(_current))
            throw new ForbiddenException("İş ataması için yetkiniz yok.");

        var task = await _db.WorkTasks.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ReviewerUser)
            .Include(t => t.Unit)
            .Include(t => t.Attachments.Where(a => !a.IsDeleted)).ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Activities).ThenInclude(a => a.ActorUser)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İş bulunamadı.");

        var me = WorkTaskAccess.RequireUser(_current);
        var canReview = WorkTaskAccess.CanReview(_current);
        if (task.CreatedByUserId != me && task.AssigneeUserId != me && task.ReviewerUserId != me && !canReview)
            throw new ForbiddenException("Bu işi görme yetkiniz yok.");

        return Map.Detail(task, me, canReview);
    }
}

public sealed class CreateWorkTaskHandler : IRequestHandler<CreateWorkTaskCommand, WorkTaskDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFileStorageService _files;

    public CreateWorkTaskHandler(AppDbContext db, ICurrentUserService current, IFileStorageService files)
    {
        _db = db;
        _current = current;
        _files = files;
    }

    public async Task<WorkTaskDetailDto> Handle(CreateWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var me = WorkTaskAccess.RequireUser(_current);
        var kind = request.Kind;

        if (kind == WorkTaskKind.Assignment && !WorkTaskAccess.CanAssign(_current))
            throw new ForbiddenException("İş atamak için müdür yetkisi gerekir.");
        if (kind == WorkTaskKind.ApprovalRequest && !WorkTaskAccess.CanSubmit(_current))
            throw new ForbiddenException("Onay talebi oluşturma yetkiniz yok.");

        Guid? assignee = request.AssigneeUserId;
        Guid? reviewer = null;

        if (kind == WorkTaskKind.Assignment)
        {
            if (assignee is null || assignee == Guid.Empty)
                throw new AppException("VALIDATION", "İşi kime atayacağınızı seçin.");
            if (assignee == me)
                throw new AppException("VALIDATION", "İşi kendinize atayamazsınız.");
            reviewer = me;
        }
        else
        {
            reviewer = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.UserRoles.Any(r => r.Role.Code == RoleCodes.Director))
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
            assignee = me;
        }

        var status = kind == WorkTaskKind.ApprovalRequest
            ? WorkTaskStatus.WaitingApproval
            : WorkTaskStatus.Open;

        var task = new WorkTask
        {
            Title = request.Title.Trim(),
            Description = (request.Description ?? "").Trim(),
            Kind = kind,
            Status = status,
            CreatedByUserId = me,
            AssigneeUserId = assignee,
            ReviewerUserId = reviewer,
            UnitId = request.UnitId,
            DueOn = request.DueOn,
            CreatedBy = _current.UserName
        };
        task.Activities.Add(new WorkTaskActivity
        {
            ActorUserId = me,
            Action = kind == WorkTaskKind.ApprovalRequest ? "submitted" : "created",
            Note = kind == WorkTaskKind.ApprovalRequest ? "Onay için gönderildi." : "İş atandı.",
            AtUtc = DateTime.UtcNow
        });

        _db.WorkTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        await SaveUploadsAsync(_db, _files, task.Id, me, _current.UserName, request.Uploads, cancellationToken);

        return await Reload(task.Id, cancellationToken);
    }

    private async Task<WorkTaskDetailDto> Reload(Guid id, CancellationToken ct)
    {
        var task = await _db.WorkTasks.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ReviewerUser)
            .Include(t => t.Unit)
            .Include(t => t.Attachments.Where(a => !a.IsDeleted)).ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Activities).ThenInclude(a => a.ActorUser)
            .FirstAsync(t => t.Id == id, ct);
        var me = WorkTaskAccess.RequireUser(_current);
        return Map.Detail(task, me, WorkTaskAccess.CanReview(_current));
    }

    internal static async Task SaveUploadsAsync(
        AppDbContext db,
        IFileStorageService files,
        Guid taskId,
        Guid userId,
        string? userName,
        IReadOnlyList<WorkTaskUpload>? uploads,
        CancellationToken ct)
    {
        if (uploads is null || uploads.Count == 0)
            return;

        foreach (var upload in uploads.Take(8))
        {
            if (upload.Content.Length == 0)
                continue;
            var type = (upload.ContentType ?? "").ToLowerInvariant();
            if (!type.StartsWith("image/") && type is not "application/pdf")
                throw new AppException("VALIDATION", "Yalnızca görsel veya PDF ekleyin.");

            var saved = await files.SaveAsync(
                upload.Content,
                upload.FileName,
                string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType,
                $"work-tasks/{taskId:N}",
                ct);

            db.WorkTaskAttachments.Add(new WorkTaskAttachment
            {
                WorkTaskId = taskId,
                UploadedByUserId = userId,
                RelativePath = saved.RelativePath,
                FileName = Path.GetFileName(upload.FileName),
                ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType,
                CreatedBy = userName
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

public sealed class SubmitWorkTaskHandler : IRequestHandler<SubmitWorkTaskCommand, WorkTaskDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFileStorageService _files;

    public SubmitWorkTaskHandler(AppDbContext db, ICurrentUserService current, IFileStorageService files)
    {
        _db = db;
        _current = current;
        _files = files;
    }

    public async Task<WorkTaskDetailDto> Handle(SubmitWorkTaskCommand request, CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanSubmit(_current))
            throw new ForbiddenException("Teslim yetkiniz yok.");

        var me = WorkTaskAccess.RequireUser(_current);
        var task = await _db.WorkTasks
            .Include(t => t.Activities)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İş bulunamadı.");

        if (task.AssigneeUserId != me && task.CreatedByUserId != me)
            throw new ForbiddenException("Bu işi teslim edemezsiniz.");
        if (task.Status is WorkTaskStatus.Approved or WorkTaskStatus.Rejected or WorkTaskStatus.Done or WorkTaskStatus.Cancelled)
            throw new AppException("VALIDATION", "Kapalı iş tekrar gönderilemez.");

        task.Status = WorkTaskStatus.WaitingApproval;
        task.UpdatedBy = _current.UserName;
        task.UpdatedAtUtc = DateTime.UtcNow;
        task.Activities.Add(new WorkTaskActivity
        {
            ActorUserId = me,
            Action = "submitted",
            Note = string.IsNullOrWhiteSpace(request.Note) ? "Onaya gönderildi." : request.Note.Trim(),
            AtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await CreateWorkTaskHandler.SaveUploadsAsync(
            _db, _files, task.Id, me, _current.UserName, request.Uploads, cancellationToken);

        var loaded = await _db.WorkTasks.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ReviewerUser)
            .Include(t => t.Unit)
            .Include(t => t.Attachments.Where(a => !a.IsDeleted)).ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Activities).ThenInclude(a => a.ActorUser)
            .FirstAsync(t => t.Id == task.Id, cancellationToken);
        return Map.Detail(loaded, me, WorkTaskAccess.CanReview(_current));
    }
}

public sealed class ReviewWorkTaskHandler : IRequestHandler<ReviewWorkTaskCommand, WorkTaskDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public ReviewWorkTaskHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<WorkTaskDetailDto> Handle(ReviewWorkTaskCommand request, CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanReview(_current))
            throw new ForbiddenException("Onay yetkisi yalnızca müdürdedir.");

        var me = WorkTaskAccess.RequireUser(_current);
        var task = await _db.WorkTasks
            .Include(t => t.Activities)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İş bulunamadı.");

        if (task.Status is not (WorkTaskStatus.WaitingApproval or WorkTaskStatus.RevisionRequested or WorkTaskStatus.InProgress or WorkTaskStatus.Open))
            throw new AppException("VALIDATION", "Bu iş şu anda onaylanamaz.");

        var decision = request.Decision.Trim().ToLowerInvariant();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        (task.Status, var action, var fallback) = decision switch
        {
            "approve" => (WorkTaskStatus.Approved, "approved", "Onaylandı."),
            "reject" => (WorkTaskStatus.Rejected, "rejected", "Reddedildi."),
            "revision" => (WorkTaskStatus.RevisionRequested, "revision", "Revizyon istendi."),
            _ => throw new AppException("VALIDATION", "Onay, red veya revizyon seçin.")
        };

        if (task.Status is WorkTaskStatus.Rejected or WorkTaskStatus.RevisionRequested && string.IsNullOrWhiteSpace(note))
            throw new AppException("VALIDATION", "Açıklama yazın.");

        task.ReviewNote = note;
        task.ReviewedAtUtc = DateTime.UtcNow;
        task.ReviewerUserId ??= me;
        task.UpdatedBy = _current.UserName;
        task.UpdatedAtUtc = DateTime.UtcNow;
        if (task.Status == WorkTaskStatus.Approved)
            task.Status = task.Kind == WorkTaskKind.Assignment ? WorkTaskStatus.Done : WorkTaskStatus.Approved;

        task.Activities.Add(new WorkTaskActivity
        {
            ActorUserId = me,
            Action = action,
            Note = note ?? fallback,
            AtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        var loaded = await _db.WorkTasks.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ReviewerUser)
            .Include(t => t.Unit)
            .Include(t => t.Attachments.Where(a => !a.IsDeleted)).ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Activities).ThenInclude(a => a.ActorUser)
            .FirstAsync(t => t.Id == task.Id, cancellationToken);
        return Map.Detail(loaded, me, true);
    }
}

public sealed class CancelWorkTaskHandler : IRequestHandler<CancelWorkTaskCommand, WorkTaskDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public CancelWorkTaskHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<WorkTaskDetailDto> Handle(CancelWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var me = WorkTaskAccess.RequireUser(_current);
        var canReview = WorkTaskAccess.CanReview(_current);
        var task = await _db.WorkTasks
            .Include(t => t.Activities)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İş bulunamadı.");

        if (task.CreatedByUserId != me && !canReview)
            throw new ForbiddenException("Bu işi iptal etme yetkiniz yok.");
        if (!WorkTaskLifecycle.CanCancel(task.Status))
            throw new AppException("VALIDATION", "Kapalı iş iptal edilemez.");

        task.Status = WorkTaskStatus.Cancelled;
        task.UpdatedBy = _current.UserName;
        task.UpdatedAtUtc = DateTime.UtcNow;
        task.Activities.Add(new WorkTaskActivity
        {
            ActorUserId = me,
            Action = "cancelled",
            Note = string.IsNullOrWhiteSpace(request.Note) ? "İş iptal edildi." : request.Note.Trim(),
            AtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        var loaded = await _db.WorkTasks.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ReviewerUser)
            .Include(t => t.Unit)
            .Include(t => t.Attachments.Where(a => !a.IsDeleted)).ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Activities).ThenInclude(a => a.ActorUser)
            .FirstAsync(t => t.Id == task.Id, cancellationToken);
        return Map.Detail(loaded, me, canReview);
    }
}

public sealed class DeleteWorkTaskHandler : IRequestHandler<DeleteWorkTaskCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public DeleteWorkTaskHandler(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(DeleteWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var me = WorkTaskAccess.RequireUser(_current);
        var canReview = WorkTaskAccess.CanReview(_current);
        var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İş bulunamadı.");

        if (task.CreatedByUserId != me && !canReview)
            throw new ForbiddenException("Bu işi silme yetkiniz yok.");
        if (!WorkTaskLifecycle.CanDelete(task.Status))
            throw new AppException("VALIDATION", "Yalnızca açık veya iptal edilmiş iş silinebilir.");

        task.IsDeleted = true;
        task.DeletedAtUtc = DateTime.UtcNow;
        task.DeletedBy = _current.UserName;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class OpenWorkTaskAttachmentHandler
    : IRequestHandler<OpenWorkTaskAttachmentQuery, WorkTaskAttachmentFileDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFileStorageService _files;

    public OpenWorkTaskAttachmentHandler(AppDbContext db, ICurrentUserService current, IFileStorageService files)
    {
        _db = db;
        _current = current;
        _files = files;
    }

    public async Task<WorkTaskAttachmentFileDto> Handle(
        OpenWorkTaskAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        if (!WorkTaskAccess.CanView(_current))
            throw new ForbiddenException();

        var me = WorkTaskAccess.RequireUser(_current);
        var canReview = WorkTaskAccess.CanReview(_current);
        var att = await _db.WorkTaskAttachments.AsNoTracking()
            .Include(a => a.WorkTask)
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.WorkTaskId == request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Dosya bulunamadı.");

        var t = att.WorkTask;
        if (t.CreatedByUserId != me && t.AssigneeUserId != me && t.ReviewerUserId != me && !canReview)
            throw new ForbiddenException();

        var opened = await _files.OpenReadAsync(att.RelativePath, cancellationToken)
            ?? throw new NotFoundException("Dosya bulunamadı.");

        return new WorkTaskAttachmentFileDto
        {
            Stream = opened.Stream,
            ContentType = string.IsNullOrWhiteSpace(att.ContentType) ? opened.ContentType : att.ContentType,
            DownloadFileName = att.FileName
        };
    }
}

file static class Map
{
    public static WorkTaskDetailDto Detail(WorkTask t, Guid me, bool canReview) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Kind = t.Kind,
        KindLabel = WorkTaskLabels.Kind(t.Kind),
        Status = t.Status,
        StatusLabel = WorkTaskLabels.Status(t.Status),
        CreatedByName = t.CreatedByUser.DisplayName,
        AssigneeName = t.AssigneeUser?.DisplayName,
        UnitName = t.Unit?.Name,
        DueOn = t.DueOn,
        CreatedAtUtc = t.CreatedAtUtc,
        AttachmentCount = t.Attachments.Count,
        CanReview = canReview && t.Status is WorkTaskStatus.WaitingApproval or WorkTaskStatus.RevisionRequested,
        CanSubmit = t.AssigneeUserId == me
            && t.Status is WorkTaskStatus.Open or WorkTaskStatus.InProgress or WorkTaskStatus.RevisionRequested,
        CanCancel = (t.CreatedByUserId == me || canReview)
            && WorkTaskLifecycle.CanCancel(t.Status),
        CanDelete = (t.CreatedByUserId == me || canReview)
            && WorkTaskLifecycle.CanDelete(t.Status),
        ReviewNote = t.ReviewNote,
        CreatedByUserId = t.CreatedByUserId,
        AssigneeUserId = t.AssigneeUserId,
        ReviewerUserId = t.ReviewerUserId,
        UnitId = t.UnitId,
        Attachments = t.Attachments
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new WorkTaskAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                UploadedByName = a.UploadedByUser.DisplayName,
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToList(),
        Activities = t.Activities
            .OrderBy(a => a.AtUtc)
            .Select(a => new WorkTaskActivityDto
            {
                Id = a.Id,
                Action = a.Action,
                ActionLabel = a.Action switch
                {
                    "created" => "Oluşturuldu",
                    "submitted" => "Onaya gönderildi",
                    "approved" => "Onaylandı",
                    "rejected" => "Reddedildi",
                    "revision" => "Revizyon istendi",
                    "cancelled" => "İptal edildi",
                    _ => a.Action
                },
                Note = a.Note,
                ActorName = a.ActorUser.DisplayName,
                AtUtc = a.AtUtc
            })
            .ToList()
    };
}
