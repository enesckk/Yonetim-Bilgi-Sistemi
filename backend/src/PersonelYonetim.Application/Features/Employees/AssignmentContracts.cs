using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Fiili görev ataması — resmi unvan (JobTitle) ile karıştırılmaz.
/// İş kuralları:
/// - Aynı anda yalnızca bir aktif ana görev (IsPrimary &amp;&amp; EndDate null).
/// - Aynı görev tanımı için örtüşen iki aktif atama olamaz.
/// </summary>
public sealed record GetAssignmentFormOptionsQuery : IRequest<AssignmentFormOptionsDto>;

public class UpsertAssignmentRequest
{
    public Guid JobDutyId { get; set; }
    public bool IsPrimary { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateAssignmentCommand : UpsertAssignmentRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateAssignmentCommand : UpsertAssignmentRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid AssignmentId { get; set; }
}

/// <summary>Soft-delete. AssignmentId must belong to EmployeeId.</summary>
public sealed record DeleteAssignmentCommand(Guid EmployeeId, Guid AssignmentId) : IRequest;

public sealed class AssignmentFormOptionsDto
{
    public IReadOnlyList<DutyLookupDto> Duties { get; init; } = [];
}

public sealed class DutyLookupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
}

public sealed class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertAssignmentRequestValidator());
    }
}

public sealed class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertAssignmentRequestValidator());
    }
}
