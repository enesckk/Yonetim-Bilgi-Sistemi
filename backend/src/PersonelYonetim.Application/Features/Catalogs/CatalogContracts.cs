using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Catalogs;

// ——— Ortak ———

public sealed class CatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public bool IsActive { get; init; }
    public int UsageCount { get; init; }
    public int? Category { get; init; }
    public string? CategoryLabel { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class CatalogGroupDto
{
    public int Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public IReadOnlyList<CatalogItemDto> Items { get; init; } = [];
}

public sealed class CatalogListDto
{
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int InUseCount { get; init; }
    public IReadOnlyList<CatalogItemDto> Items { get; init; } = [];
    public IReadOnlyList<CatalogGroupDto> Groups { get; init; } = [];
}

public sealed class CatalogOptionDto
{
    public int Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CatalogsOverviewDto
{
    public int JobDutyCount { get; init; }
    public int JobTitleCount { get; init; }
    public int EmploymentTypeCount { get; init; }
    public int FacilityCategoryCount { get; init; }
    public IReadOnlyList<CatalogOptionDto> DutyCategories { get; init; } = [];
}

public sealed record GetCatalogsOverviewQuery : IRequest<CatalogsOverviewDto>;

// ——— Fiili görev ———

public sealed record GetJobDutyCatalogQuery : IRequest<CatalogListDto>;
public sealed class CreateJobDutyCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DutyCategory Category { get; set; }
}
public sealed class UpdateJobDutyCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DutyCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record DeleteJobDutyCommand(Guid Id) : IRequest;

// ——— Unvan ———

public sealed record GetJobTitleCatalogQuery : IRequest<CatalogListDto>;
public sealed class CreateJobTitleCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}
public sealed class UpdateJobTitleCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record DeleteJobTitleCommand(Guid Id) : IRequest;

// ——— İstihdam ———

public sealed record GetEmploymentTypeCatalogQuery : IRequest<CatalogListDto>;
public sealed class CreateEmploymentTypeCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
}
public sealed class UpdateEmploymentTypeCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record DeleteEmploymentTypeCommand(Guid Id) : IRequest;

// ——— Tesis türü ———

public sealed record GetFacilityCategoryCatalogQuery : IRequest<CatalogListDto>;
public sealed class CreateFacilityCategoryCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
}
public sealed class UpdateFacilityCategoryCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record DeleteFacilityCategoryCommand(Guid Id) : IRequest;

// ——— Validators ———

public sealed class CreateJobDutyCommandValidator : AbstractValidator<CreateJobDutyCommand>
{
    public CreateJobDutyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum()
            .Must(c => Enum.IsDefined(c) && (byte)c != 0)
            .WithMessage("Geçerli bir görev kategorisi seçin.");
    }
}

public sealed class UpdateJobDutyCommandValidator : AbstractValidator<UpdateJobDutyCommand>
{
    public UpdateJobDutyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum()
            .Must(c => Enum.IsDefined(c) && (byte)c != 0)
            .WithMessage("Geçerli bir görev kategorisi seçin.");
    }
}

public sealed class CreateJobTitleCommandValidator : AbstractValidator<CreateJobTitleCommand>
{
    public CreateJobTitleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(50);
    }
}

public sealed class UpdateJobTitleCommandValidator : AbstractValidator<UpdateJobTitleCommand>
{
    public UpdateJobTitleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(50);
    }
}

public sealed class CreateEmploymentTypeCommandValidator : AbstractValidator<CreateEmploymentTypeCommand>
{
    public CreateEmploymentTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999);
    }
}

public sealed class UpdateEmploymentTypeCommandValidator : AbstractValidator<UpdateEmploymentTypeCommand>
{
    public UpdateEmploymentTypeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999);
    }
}

public sealed class CreateFacilityCategoryCommandValidator : AbstractValidator<CreateFacilityCategoryCommand>
{
    public CreateFacilityCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999);
    }
}

public sealed class UpdateFacilityCategoryCommandValidator : AbstractValidator<UpdateFacilityCategoryCommand>
{
    public UpdateFacilityCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999);
    }
}
