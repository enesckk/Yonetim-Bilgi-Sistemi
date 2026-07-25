using FluentValidation;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertCertificateRequestValidator : AbstractValidator<UpsertCertificateRequest>
{
    public UpsertCertificateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sertifika adı zorunludur.")
            .When(x => !x.CertificateDefinitionId.HasValue);

        RuleFor(x => x.Name).MaximumLength(200);

        RuleFor(x => x.Issuer).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.DocumentNumber).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.ExpiresOn)
            .GreaterThanOrEqualTo(x => x.IssuedOn!.Value)
            .WithMessage("Bitiş tarihi veriliş tarihinden önce olamaz.")
            .When(x => x.ExpiresOn.HasValue && x.IssuedOn.HasValue);

        RuleFor(x => x.IssuedOn)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
            .WithMessage("Veriliş tarihi gelecekte olamaz.")
            .When(x => x.IssuedOn.HasValue);
    }
}
