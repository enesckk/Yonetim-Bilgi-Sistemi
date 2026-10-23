using MediatR;

namespace PersonelYonetim.Application.Features.Messages;

public sealed record MessageArchivePage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record AdminConversationDto(
    MessageUserDto FirstUser, MessageUserDto SecondUser, int MessageCount,
    DateTime LastAtUtc, Guid LastSenderUserId, string LastBody);
public sealed record GetAdminConversationsQuery(string? Search = null, int Page = 1)
    : IRequest<MessageArchivePage<AdminConversationDto>>;
public sealed record GetAdminConversationQuery(Guid FirstUserId, Guid SecondUserId, int Page = 1)
    : IRequest<MessageArchivePage<DirectMessageDto>>;
public sealed record OpenAdminMessageAttachmentQuery(Guid MessageId) : IRequest<MessageAttachmentFileDto>;
