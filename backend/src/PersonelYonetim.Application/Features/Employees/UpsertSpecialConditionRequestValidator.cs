using FluentValidation;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertSpecialConditionRequestValidator : AbstractValidator<UpsertSpecialConditionRequest>
{
    public UpsertSpecialConditionRequestValidator()
    {
        RuleFor(x => x.ConditionType)
            .NotEmpty().WithMessage("Durum tipi zorunludur.")
            .MaximumLength(150);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Açıklama zorunludur.")
            .MaximumLength(2000);

        // Sürekli kayıtta bitiş tarihi olmamalı (iş kuralı)
        RuleFor(x => x.EndDate)
            .Null()
            .WithMessage("Sürekli özel durumda bitiş tarihi girilmez.")
            .When(x => x.IsPermanent);

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.")
            .When(x => !x.IsPermanent && x.EndDate.HasValue && x.StartDate.HasValue);
    }
}
