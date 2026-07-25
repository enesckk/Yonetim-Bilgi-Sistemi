using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertEducationRequestValidator : AbstractValidator<UpsertEducationRequest>
{
    public UpsertEducationRequestValidator()
    {
        RuleFor(x => x.Level)
            .IsInEnum()
            .Must(l => l != EducationLevel.Unknown)
            .WithMessage("Eğitim seviyesi seçilmelidir.");

        RuleFor(x => x.CompletionStatus)
            .IsInEnum();

        RuleFor(x => x.University).MaximumLength(200);
        RuleFor(x => x.Faculty).MaximumLength(200);
        RuleFor(x => x.School).MaximumLength(200);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.Program).MaximumLength(200);
        RuleFor(x => x.DiplomaNumber).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.GraduationYear)
            .InclusiveBetween((short)1950, (short)(DateTime.UtcNow.Year + 10))
            .WithMessage($"Mezuniyet yılı 1950–{DateTime.UtcNow.Year + 10} arasında olmalıdır.")
            .When(x => x.GraduationYear.HasValue);

        // İş kuralı: mezun ise yıl genelde beklenir (zorunlu değil ama uyarı seviyesinde değil — soft zorunluluk)
        RuleFor(x => x.GraduationYear)
            .NotNull()
            .WithMessage("Mezun durumunda mezuniyet yılı girilmesi önerilir; lütfen yılı doldurun.")
            .When(x => x.CompletionStatus == EducationCompletionStatus.Graduated);
    }
}
