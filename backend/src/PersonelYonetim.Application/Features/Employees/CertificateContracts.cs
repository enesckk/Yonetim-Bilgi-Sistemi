using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel sertifikası — MediatR dilimi, hibrit model:
/// Catalog (CertificateDefinition) opsiyonel + serbest Name (katalog dışı belgeler için).
/// Yetkinlikteki HasCertificate bayrağından farklıdır (asıl belge kaydı burasıdır).
/// </summary>
public sealed record GetCertificateFormOptionsQuery : IRequest<CertificateFormOptionsDto>;

public class UpsertCertificateRequest
{
    /// <summary>Opsiyonel katalog. Seçilirse Name/Category boşsa tanımdan doldurulur.</summary>
    public Guid? CertificateDefinitionId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? Category { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }

    /// <summary>İlişkili yetkinlik (katalog Skill) — opsiyonel.</summary>
    public Guid? RelatedSkillId { get; set; }
}

public sealed class CreateCertificateCommand : UpsertCertificateRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateCertificateCommand : UpsertCertificateRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid CertificateId { get; set; }
}

/// <summary>Soft-delete. CertificateId mutlaka EmployeeId'ye ait olmalı.</summary>
public sealed record DeleteCertificateCommand(Guid EmployeeId, Guid CertificateId) : IRequest;

public sealed class CertificateFormOptionsDto
{
    public IReadOnlyList<LookupItemDto> Definitions { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Skills { get; init; } = [];
}

public sealed class CreateCertificateCommandValidator : AbstractValidator<CreateCertificateCommand>
{
    public CreateCertificateCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertCertificateRequestValidator());
    }
}

public sealed class UpdateCertificateCommandValidator : AbstractValidator<UpdateCertificateCommand>
{
    public UpdateCertificateCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.CertificateId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertCertificateRequestValidator());
    }
}
