using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class UpsertNoteRequestValidator : AbstractValidator<UpsertNoteRequest>
{
    public UpsertNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Başlık zorunludur.")
            .MaximumLength(200);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Not içeriği zorunludur.")
            .MaximumLength(4000);

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Visibility).IsInEnum();

        RuleFor(x => x.ReminderDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(-1)))
            .When(x => x.ReminderDate.HasValue);
    }
}
