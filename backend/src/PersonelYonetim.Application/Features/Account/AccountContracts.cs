using FluentValidation;
using MediatR;
using PersonelYonetim.Application.Common.Validation;

namespace PersonelYonetim.Application.Features.Account;

/// <summary>Oturum açan kullanıcının kendi hesap bilgileri (self-service).</summary>
public sealed record GetMyAccountQuery : IRequest<MyAccountDto>;

public sealed class MyAccountDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime? LastLoginAtUtc { get; init; }
    public IReadOnlyList<string> RoleNames { get; init; } = [];
    public string? EmployeeName { get; init; }
}

public sealed class UpdateMyAccountCommand : IRequest
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class ChangeMyPasswordCommand : IRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UpdateMyAccountCommandValidator : AbstractValidator<UpdateMyAccountCommand>
{
    public UpdateMyAccountCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .WithMessage("Kullanıcı adı yalnızca harf, rakam, . _ - içerebilir.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut şifrenizi girin.");
        RuleFor(x => x.NewPassword).ApplyPasswordPolicy();
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Yeni şifre mevcut şifreyle aynı olamaz.");
    }
}
