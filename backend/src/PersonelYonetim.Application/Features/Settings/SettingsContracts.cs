using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Settings;

public sealed record GetAppSettingsQuery : IRequest<IReadOnlyList<AppSettingDto>>;

public sealed class UpdateAppSettingCommand : IRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class AppSettingDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public AppSettingValueType ValueType { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsReadOnly { get; init; }
}

public sealed class UpdateAppSettingCommandValidator : AbstractValidator<UpdateAppSettingCommand>
{
    public UpdateAppSettingCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotNull().MaximumLength(2000);
    }
}
