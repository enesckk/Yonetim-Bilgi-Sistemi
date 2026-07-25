using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertEmployeeSkillRequestValidator : AbstractValidator<UpsertEmployeeSkillRequest>
{
    public UpsertEmployeeSkillRequestValidator()
    {
        RuleFor(x => x.SkillId)
            .NotEmpty().WithMessage("Yetkinlik seçilmelidir.");

        RuleFor(x => x.Level)
            .IsInEnum()
            .WithMessage("Geçerli bir seviye seçin.");

        RuleFor(x => x.ExperienceDuration).MaximumLength(100);
        RuleFor(x => x.CertificateIssuer).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);

        // Sertifika var denmişse tarih / kurum tutarlılığı
        RuleFor(x => x.CertificateDate)
            .NotNull()
            .WithMessage("Sertifika varsa tarih girilmelidir.")
            .When(x => x.HasCertificate);

        RuleFor(x => x.CertificateDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
            .WithMessage("Sertifika tarihi gelecekte olamaz.")
            .When(x => x.CertificateDate.HasValue);

        RuleFor(x => x.CertificateIssuer)
            .Empty()
            .WithMessage("Sertifika yokken kurum bilgisi girilmez.")
            .When(x => !x.HasCertificate && !string.IsNullOrWhiteSpace(x.CertificateIssuer));
    }
}
