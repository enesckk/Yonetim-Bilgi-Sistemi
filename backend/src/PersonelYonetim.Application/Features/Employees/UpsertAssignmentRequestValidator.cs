using FluentValidation;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertAssignmentRequestValidator : AbstractValidator<UpsertAssignmentRequest>
{
    public UpsertAssignmentRequestValidator()
    {
        RuleFor(x => x.JobDutyId)
            .NotEmpty().WithMessage("Görev seçilmelidir.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Başlangıç tarihi zorunludur.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
