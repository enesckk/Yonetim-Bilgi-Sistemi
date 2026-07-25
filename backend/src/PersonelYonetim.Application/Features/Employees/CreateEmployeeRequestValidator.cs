using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// FluentValidation: kurallar tek yerde, okunabilir ve test edilebilir.
/// Controller'da if-else yığını yerine declarative (bildirimsel) doğrulama.
/// </summary>
public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.EmployeeNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.EmployeeNumber));

        RuleFor(x => x.PersonalEmail)
            .EmailAddress().WithMessage("Geçerli bir e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));

        RuleFor(x => x.CorporateEmail)
            .EmailAddress().WithMessage("Geçerli bir kurumsal e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CorporateEmail));

        RuleFor(x => x.PersonalPhone).MaximumLength(30);
        RuleFor(x => x.CorporatePhone).MaximumLength(30);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(30);

        RuleFor(x => x.NationalId)
            .Must(BeValidTurkishNationalId)
            .WithMessage("T.C. kimlik numarası hatalı (kontrol hanesi uyuşmuyor). 11 haneli geçerli bir numara girin.")
            .When(x => !string.IsNullOrWhiteSpace(x.NationalId));

        RuleFor(x => x.Gender)
            .IsInEnum();

        RuleFor(x => x.Status)
            .IsInEnum()
            .Must(s => Enum.IsDefined(s) && (byte)s != 0)
            .WithMessage("Geçerli bir durum seçin.");

        RuleFor(x => x.BirthDate)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Doğum tarihi bugünden önce olmalıdır.")
            .When(x => x.BirthDate.HasValue);

        RuleFor(x => x.HireDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddDays(30)))
            .WithMessage("İşe giriş tarihi çok ileri bir tarih olamaz.")
            .When(x => x.HireDate.HasValue);
    }

    /// <summary>
    /// TCKN algoritması (mod 10). Üretimde Mernis doğrulaması ayrı bir entegrasyon olur.
    /// </summary>
    internal static bool BeValidTurkishNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 11 || digits[0] == '0')
            return false;

        // Tüm haneleri aynı olan numaralar geçersiz sayılır
        if (digits.Distinct().Count() == 1)
            return false;

        var d = digits.Select(c => c - '0').ToArray();
        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];
        var dig10 = ((oddSum * 7) - evenSum) % 10;
        if (dig10 < 0) dig10 += 10;
        if (d[9] != dig10)
            return false;

        var dig11 = d.Take(10).Sum() % 10;
        return d[10] == dig11;
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.EmployeeNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.EmployeeNumber));

        RuleFor(x => x.PersonalEmail)
            .EmailAddress().WithMessage("Geçerli bir e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));

        RuleFor(x => x.CorporateEmail)
            .EmailAddress().WithMessage("Geçerli bir kurumsal e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CorporateEmail));

        RuleFor(x => x.PersonalPhone).MaximumLength(30);
        RuleFor(x => x.CorporatePhone).MaximumLength(30);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(30);

        RuleFor(x => x.NationalId)
            .Must(CreateEmployeeRequestValidator.BeValidTurkishNationalId)
            .WithMessage("T.C. kimlik numarası hatalı (kontrol hanesi uyuşmuyor). 11 haneli geçerli bir numara girin.")
            .When(x => x.NationalIdProvided && !string.IsNullOrWhiteSpace(x.NationalId));

        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Status)
            .IsInEnum()
            .Must(s => Enum.IsDefined(s) && (byte)s != 0)
            .WithMessage("Geçerli bir durum seçin.");

        RuleFor(x => x.BirthDate)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
            .When(x => x.BirthDate.HasValue);
    }
}
