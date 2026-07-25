using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel–yetkinlik — MediatR dilimi (eski ISkillCommandService yerine).
/// Query = okuma; Command = yazma. Controller yalnızca ISender.Send çağırır.
/// </summary>
public sealed record GetSkillFormOptionsQuery : IRequest<SkillFormOptionsDto>;

/// <summary>
/// Ortak alanlar — Create/Update gövdesi.
/// Öğrenme: Request DTO kalır; Command onu taşır + EmployeeId ekler.
/// </summary>
public class UpsertEmployeeSkillRequest
{
    public Guid SkillId { get; set; }
    public SkillLevel Level { get; set; }
    public string? ExperienceDuration { get; set; }
    public bool HasCertificate { get; set; }
    public DateOnly? CertificateDate { get; set; }
    public string? CertificateIssuer { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateEmployeeSkillCommand : UpsertEmployeeSkillRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateEmployeeSkillCommand : UpsertEmployeeSkillRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid EmployeeSkillId { get; set; }
}

public sealed record DeleteEmployeeSkillCommand(Guid EmployeeId, Guid EmployeeSkillId) : IRequest;

public sealed class SkillFormOptionsDto
{
    public IReadOnlyList<SkillLookupDto> Skills { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> Levels { get; init; } = [];
}

public sealed class SkillLookupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
}

public sealed class CreateEmployeeSkillCommandValidator : AbstractValidator<CreateEmployeeSkillCommand>
{
    public CreateEmployeeSkillCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        // Ortak alan kuralları — ValidationBehavior handler öncesi çalıştırır
        RuleFor(x => x).SetValidator(new UpsertEmployeeSkillRequestValidator());
    }
}

public sealed class UpdateEmployeeSkillCommandValidator : AbstractValidator<UpdateEmployeeSkillCommand>
{
    public UpdateEmployeeSkillCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.EmployeeSkillId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertEmployeeSkillRequestValidator());
    }
}
