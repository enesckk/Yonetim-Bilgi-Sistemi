using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel → Eğitim (aggregate child) — MediatR dilimi.
/// Skills’ten fark: serbest metin (üniversite/bölüm); katalog SkillId yok.
/// </summary>
public sealed record GetEducationFormOptionsQuery : IRequest<EducationFormOptionsDto>;

public class UpsertEducationRequest
{
    public EducationLevel Level { get; set; }
    public string? University { get; set; }
    public string? Faculty { get; set; }
    public string? School { get; set; }
    public string? Department { get; set; }
    public string? Program { get; set; }
    public short? GraduationYear { get; set; }
    public EducationCompletionStatus CompletionStatus { get; set; } = EducationCompletionStatus.Unknown;
    public string? DiplomaNumber { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateEducationCommand : UpsertEducationRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateEducationCommand : UpsertEducationRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid EducationId { get; set; }
}

/// <summary>Soft-delete. Parent-child + IDOR: EducationId mutlaka EmployeeId’ye ait olmalı.</summary>
public sealed record DeleteEducationCommand(Guid EmployeeId, Guid EducationId) : IRequest;

public sealed class EducationFormOptionsDto
{
    public IReadOnlyList<EnumOptionDto> Levels { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> CompletionStatuses { get; init; } = [];
}

public sealed class CreateEducationCommandValidator : AbstractValidator<CreateEducationCommand>
{
    public CreateEducationCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertEducationRequestValidator());
    }
}

public sealed class UpdateEducationCommandValidator : AbstractValidator<UpdateEducationCommand>
{
    public UpdateEducationCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.EducationId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertEducationRequestValidator());
    }
}
