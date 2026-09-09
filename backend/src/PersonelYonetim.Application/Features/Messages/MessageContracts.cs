using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Messages;

public sealed class MessageUserDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ConversationDto
{
    public Guid OtherUserId { get; init; }
    public string OtherUserName { get; init; } = string.Empty;
    public string OtherDisplayName { get; init; } = string.Empty;
    public string LastBody { get; init; } = string.Empty;
    public DateTime LastAtUtc { get; init; }
    public int UnreadCount { get; init; }
}

public sealed class DirectMessageDto
{
    public Guid Id { get; init; }
    public Guid SenderUserId { get; init; }
    public Guid RecipientUserId { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; init; }
    public bool Mine { get; init; }
    public bool HasAttachment { get; init; }
    public string? AttachmentFileName { get; init; }
    public string? AttachmentContentType { get; init; }
    public Guid? RelatedEventId { get; init; }
}

public sealed class MessageAttachmentFileDto
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }
}

public sealed record GetMessageDirectoryQuery(string? Search = null)
    : IRequest<IReadOnlyList<MessageUserDto>>;

public sealed record GetConversationsQuery : IRequest<IReadOnlyList<ConversationDto>>;

public sealed record GetConversationMessagesQuery(Guid OtherUserId)
    : IRequest<IReadOnlyList<DirectMessageDto>>;

public sealed record GetUnreadMessageCountQuery : IRequest<int>;

public sealed record OpenMessageAttachmentQuery(Guid MessageId) : IRequest<MessageAttachmentFileDto>;

public sealed class SendDirectMessageCommand : IRequest<DirectMessageDto>
{
    public Guid RecipientUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public Stream? Attachment { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public Guid? RelatedEventId { get; set; }
}

public sealed class SendDirectMessageValidator : AbstractValidator<SendDirectMessageCommand>
{
    public SendDirectMessageValidator()
    {
        RuleFor(x => x.RecipientUserId).NotEmpty().WithMessage("Alıcı seçin.");
        RuleFor(x => x.Body).MaximumLength(4000);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Body) || x.Attachment is not null)
            .WithMessage("Mesaj metni veya PDF/görsel ekleyin.");
    }
}
