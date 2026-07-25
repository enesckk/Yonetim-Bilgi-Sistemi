using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Certificates;

public sealed record GetCertificateCatalogQuery : IRequest<CertificateCatalogDto>;

public sealed record GetCertificateCatalogDetailQuery(Guid Id) : IRequest<CertificateCatalogDetailDto?>;

public sealed class CertificateCatalogDto
{
    public int TotalDefinitions { get; init; }
    public int ActiveDefinitions { get; init; }
    public int TotalAssignments { get; init; }
    public int ExpiredCount { get; init; }
    public int ExpiringSoonCount { get; init; }
    public int EmployeesWithoutCertificates { get; init; }
    public IReadOnlyList<CertificateCategoryGroupDto> Categories { get; init; } = [];
}

public sealed class CertificateCategoryGroupDto
{
    public string Category { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public IReadOnlyList<CertificateCatalogItemDto> Certificates { get; init; } = [];
}

public sealed class CertificateCatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int EmployeeCount { get; init; }
    public int ExpiredCount { get; init; }
    public int ExpiringSoonCount { get; init; }
    public int ValidCount { get; init; }
}

public sealed class CertificateCatalogDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int EmployeeCount { get; init; }
    public int ExpiredCount { get; init; }
    public int ExpiringSoonCount { get; init; }
    public int ValidCount { get; init; }
    public IReadOnlyList<CertificateEmployeeItemDto> Employees { get; init; } = [];
}

public sealed class CertificateEmployeeItemDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? UnitName { get; init; }
    public string? JobTitleName { get; init; }
    public string? Issuer { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    /// <summary>valid | expiring | expired | open</summary>
    public string ExpiryStatus { get; init; } = "open";
    public string ExpiryStatusLabel { get; init; } = string.Empty;
}

public sealed class CreateCertificateDefinitionCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class UpdateCertificateDefinitionCommand : IRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record DeleteCertificateDefinitionCommand(Guid Id) : IRequest;

public sealed class CreateCertificateDefinitionCommandValidator
    : AbstractValidator<CreateCertificateDefinitionCommand>
{
    public CreateCertificateDefinitionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sertifika adı zorunludur.")
            .MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}

public sealed class UpdateCertificateDefinitionCommandValidator
    : AbstractValidator<UpdateCertificateDefinitionCommand>
{
    public UpdateCertificateDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sertifika adı zorunludur.")
            .MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}
