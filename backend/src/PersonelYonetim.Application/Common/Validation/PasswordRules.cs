using FluentValidation;

namespace PersonelYonetim.Application.Common.Validation;

/// <summary>
/// Kurumsal şifre kuralları — kullanıcı oluşturma ve şifre sıfırlama ortak.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    public const string PolicyMessage =
        "Şifre en az 8 karakter olmalı; en az bir büyük harf, bir küçük harf, bir rakam ve bir özel karakter içermelidir.";

    public static IRuleBuilderOptions<T, string> ApplyPasswordPolicy<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .MinimumLength(MinLength)
            .Matches(@"[A-Z]").WithMessage(PolicyMessage)
            .Matches(@"[a-z]").WithMessage(PolicyMessage)
            .Matches(@"[0-9]").WithMessage(PolicyMessage)
            .Matches(@"[^a-zA-Z0-9]").WithMessage(PolicyMessage);
}
