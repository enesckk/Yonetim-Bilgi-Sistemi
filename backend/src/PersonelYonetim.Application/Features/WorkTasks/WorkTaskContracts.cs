using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.WorkTasks;

public sealed class WorkTaskUserDto
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
}

public sealed class WorkTaskAttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class WorkTaskActivityDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime AtUtc { get; init; }
}

public class WorkTaskListRowDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public WorkTaskKind Kind { get; init; }
    public string KindLabel { get; init; } = string.Empty;
    public WorkTaskStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public string CreatedByName { get; init; } = string.Empty;
    public string? AssigneeName { get; init; }
    public string? UnitName { get; init; }
    public DateTime? DueOn { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public int AttachmentCount { get; init; }
    public bool CanReview { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanCancel { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class WorkTaskDetailDto : WorkTaskListRowDto
{
    public string Description { get; init; } = string.Empty;
    public string? ReviewNote { get; init; }
    public Guid CreatedByUserId { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public Guid? ReviewerUserId { get; init; }
    public Guid? UnitId { get; init; }
    public IReadOnlyList<WorkTaskAttachmentDto> Attachments { get; init; } = [];
    public IReadOnlyList<WorkTaskActivityDto> Activities { get; init; } = [];
}

public sealed class WorkTaskAttachmentFileDto
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }
}

public sealed record GetWorkTaskOptionsQuery : IRequest<IReadOnlyList<WorkTaskUserDto>>;

public sealed record GetWorkTasksQuery(string? Tab = null) : IRequest<IReadOnlyList<WorkTaskListRowDto>>;

public sealed record GetWorkTaskQuery(Guid Id) : IRequest<WorkTaskDetailDto>;

public sealed record OpenWorkTaskAttachmentQuery(Guid TaskId, Guid AttachmentId)
    : IRequest<WorkTaskAttachmentFileDto>;

public sealed class CreateWorkTaskCommand : IRequest<WorkTaskDetailDto>
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkTaskKind Kind { get; set; } = WorkTaskKind.Assignment;
    public Guid? AssigneeUserId { get; set; }
    public Guid? UnitId { get; set; }
    public DateTime? DueOn { get; set; }
    public IReadOnlyList<WorkTaskUpload>? Uploads { get; set; }
}

public sealed class WorkTaskUpload
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}

public sealed class SubmitWorkTaskCommand : IRequest<WorkTaskDetailDto>
{
    public Guid Id { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<WorkTaskUpload>? Uploads { get; set; }
}

public sealed class ReviewWorkTaskCommand : IRequest<WorkTaskDetailDto>
{
    public Guid Id { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class CancelWorkTaskCommand : IRequest<WorkTaskDetailDto>
{
    public Guid Id { get; set; }
    public string? Note { get; set; }
}

public sealed class DeleteWorkTaskCommand : IRequest
{
    public Guid Id { get; set; }
}

public sealed class CancelWorkTaskValidator : AbstractValidator<CancelWorkTaskCommand>
{
    public CancelWorkTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class CreateWorkTaskValidator : AbstractValidator<CreateWorkTaskCommand>
{
    public CreateWorkTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("İş başlığı yazın.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class ReviewWorkTaskValidator : AbstractValidator<ReviewWorkTaskCommand>
{
    public ReviewWorkTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Decision)
            .Must(d => d is "approve" or "reject" or "revision")
            .WithMessage("Onay, red veya revizyon seçin.");
        RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => x.Decision is "reject" or "revision")
            .WithMessage("Red veya revizyon için açıklama yazın.");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
