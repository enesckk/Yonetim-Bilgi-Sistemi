using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel hareket geçmişi — zaman çizelgesi / audit trail.
/// Atama değişince otomatik kayıt da üretilebilir (yan etki, aynı transaction).
/// </summary>
public sealed record GetMovementFormOptionsQuery : IRequest<MovementFormOptionsDto>;

public class CreateMovementRequest
{
    public MovementType MovementType { get; set; }
    public Guid? OldUnitId { get; set; }
    public Guid? NewUnitId { get; set; }
    public Guid? OldFacilityId { get; set; }
    public Guid? NewFacilityId { get; set; }
    public Guid? OldJobTitleId { get; set; }
    public Guid? NewJobTitleId { get; set; }
    public Guid? OldJobDutyId { get; set; }
    public Guid? NewJobDutyId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public string? ApprovedBy { get; set; }
}

public sealed class CreateMovementCommand : CreateMovementRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateMovementCommand : CreateMovementRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid MovementId { get; set; }
}

/// <summary>Soft-delete. MovementId must belong to EmployeeId.</summary>
public sealed record DeleteMovementCommand(Guid EmployeeId, Guid MovementId) : IRequest;

public sealed class MovementFormOptionsDto
{
    public IReadOnlyList<EnumOptionDto> MovementTypes { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Units { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Facilities { get; init; } = [];
    public IReadOnlyList<LookupItemDto> JobTitles { get; init; } = [];
    public IReadOnlyList<DutyLookupDto> Duties { get; init; } = [];
}

public sealed class CreateMovementCommandValidator : AbstractValidator<CreateMovementCommand>
{
    public CreateMovementCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new CreateMovementRequestValidator());
    }
}

public sealed class UpdateMovementCommandValidator : AbstractValidator<UpdateMovementCommand>
{
    public UpdateMovementCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.MovementId).NotEmpty();
        RuleFor(x => x).SetValidator(new CreateMovementRequestValidator());
    }
}
