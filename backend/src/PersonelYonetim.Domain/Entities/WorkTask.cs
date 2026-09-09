using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Müdürlük iş ataması / onay talebi.</summary>
public class WorkTask : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkTaskKind Kind { get; set; } = WorkTaskKind.Assignment;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Open;

    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;

    public Guid? AssigneeUserId { get; set; }
    public AppUser? AssigneeUser { get; set; }

    public Guid? ReviewerUserId { get; set; }
    public AppUser? ReviewerUser { get; set; }

    public Guid? UnitId { get; set; }
    public OrganizationUnit? Unit { get; set; }

    public DateTime? DueOn { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public ICollection<WorkTaskAttachment> Attachments { get; set; } = new List<WorkTaskAttachment>();
    public ICollection<WorkTaskActivity> Activities { get; set; } = new List<WorkTaskActivity>();
}

public class WorkTaskAttachment : AuditableEntity
{
    public Guid WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;

    public Guid UploadedByUserId { get; set; }
    public AppUser UploadedByUser { get; set; } = null!;

    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

public class WorkTaskActivity : BaseEntity
{
    public Guid WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;

    public Guid ActorUserId { get; set; }
    public AppUser ActorUser { get; set; } = null!;

    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
