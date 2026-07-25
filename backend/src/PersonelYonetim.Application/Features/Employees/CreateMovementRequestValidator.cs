using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class CreateMovementRequestValidator : AbstractValidator<CreateMovementRequest>
{
    public CreateMovementRequestValidator()
    {
        RuleFor(x => x.MovementType)
            .IsInEnum()
            .WithMessage("Hareket türü seçilmelidir.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Başlangıç tarihi zorunludur.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ApprovedBy).MaximumLength(150);

        // En az bir bağlam alanı veya açıklama — boş hareket engellenir
        RuleFor(x => x)
            .Must(HasMeaningfulContent)
            .WithMessage("Hareket için neden, açıklama veya en az bir birim/görev bilgisi girin.")
            .WithName("description");
    }

    private static bool HasMeaningfulContent(CreateMovementRequest x) =>
        !string.IsNullOrWhiteSpace(x.Reason)
        || !string.IsNullOrWhiteSpace(x.Description)
        || x.OldUnitId.HasValue || x.NewUnitId.HasValue
        || x.OldFacilityId.HasValue || x.NewFacilityId.HasValue
        || x.OldJobTitleId.HasValue || x.NewJobTitleId.HasValue
        || x.OldJobDutyId.HasValue || x.NewJobDutyId.HasValue
        || x.MovementType is MovementType.LeftJob or MovementType.Retirement or MovementType.ReturnToDuty;
}
