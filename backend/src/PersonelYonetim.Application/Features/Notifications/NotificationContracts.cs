using FluentValidation;
using MediatR;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Notifications;

public sealed class GetMyNotificationsQuery : IRequest<IReadOnlyList<NotificationDto>>
{
    public bool UnreadOnly { get; set; }
    public string? Category { get; set; }
    public int Take { get; set; } = 50;
}

public sealed record GetUnreadNotificationCountQuery : IRequest<int>;

public sealed class MarkNotificationReadCommand : IRequest
{
    public Guid Id { get; set; }
}

public sealed class MarkAllNotificationsReadCommand : IRequest;

/// <summary>Veri kalitesi / sertifika / tesis taramasını elle çalıştır.</summary>
public sealed record RunNotificationScanCommand : IRequest<NotificationScanResult>;

public sealed class NotificationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public NotificationSeverity Severity { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? LinkUrl { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; init; }
}

public sealed class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    public GetMyNotificationsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
