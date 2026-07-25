using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Skills;

public sealed record GetSkillCatalogQuery : IRequest<SkillCatalogDto>;

public sealed record GetSkillCatalogDetailQuery(Guid Id) : IRequest<SkillCatalogDetailDto?>;

public sealed class SkillCatalogDto
{
    public int TotalSkills { get; init; }
    public int ActiveSkills { get; init; }
    public int TotalAssignments { get; init; }
    public int EmployeesWithoutSkills { get; init; }
    public IReadOnlyList<SkillCategoryGroupDto> Categories { get; init; } = [];
}

public sealed class SkillCategoryGroupDto
{
    public int Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public IReadOnlyList<SkillCatalogItemDto> Skills { get; init; } = [];
}

public sealed class SkillCatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int EmployeeCount { get; init; }
    public int CertificateCount { get; init; }
    public IReadOnlyList<SkillLevelCountDto> LevelBreakdown { get; init; } = [];
}

public sealed class SkillLevelCountDto
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class SkillCatalogDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int EmployeeCount { get; init; }
    public int CertificateCount { get; init; }
    public IReadOnlyList<SkillLevelCountDto> LevelBreakdown { get; init; } = [];
    public IReadOnlyList<SkillEmployeeItemDto> Employees { get; init; } = [];
}

public sealed class SkillEmployeeItemDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? UnitName { get; init; }
    public string? JobTitleName { get; init; }
    public string LevelLabel { get; init; } = string.Empty;
    public bool HasCertificate { get; init; }
    public string? ExperienceDuration { get; init; }
}

public sealed class CreateSkillCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
}

public sealed class UpdateSkillCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record DeleteSkillCommand(Guid Id) : IRequest;

public sealed class CreateSkillCommandValidator : AbstractValidator<CreateSkillCommand>
{
    public CreateSkillCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Yetkinlik adı zorunludur.")
            .MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum()
            .Must(c => Enum.IsDefined(c) && (byte)c != 0)
            .WithMessage("Geçerli bir kategori seçin.");
    }
}

public sealed class UpdateSkillCommandValidator : AbstractValidator<UpdateSkillCommand>
{
    public UpdateSkillCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Yetkinlik adı zorunludur.")
            .MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum()
            .Must(c => Enum.IsDefined(c) && (byte)c != 0)
            .WithMessage("Geçerli bir kategori seçin.");
    }
}
