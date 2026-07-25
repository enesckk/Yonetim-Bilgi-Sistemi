using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel notları — görünürlük seviyeli, satır bazlı erişim için MediatR dilimi.
/// </summary>
public sealed record GetNoteFormOptionsQuery : IRequest<NoteFormOptionsDto>;

public class UpsertNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public NoteCategory Category { get; set; } = NoteCategory.General;
    public string Content { get; set; } = string.Empty;
    public NoteVisibility Visibility { get; set; } = NoteVisibility.UnitManagers;
    public DateOnly? ReminderDate { get; set; }
}

public sealed class CreateNoteCommand : UpsertNoteRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateNoteCommand : UpsertNoteRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid NoteId { get; set; }
}

/// <summary>Soft-delete. Parent-child + IDOR: NoteId mutlaka EmployeeId’ye ait olmalı.</summary>
public sealed record DeleteNoteCommand(Guid EmployeeId, Guid NoteId) : IRequest;

public sealed class NoteFormOptionsDto
{
    public IReadOnlyList<EnumOptionDto> Categories { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> Visibilities { get; init; } = [];
}

public sealed class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertNoteRequestValidator());
    }
}

public sealed class UpdateNoteCommandValidator : AbstractValidator<UpdateNoteCommand>
{
    public UpdateNoteCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertNoteRequestValidator());
    }
}
