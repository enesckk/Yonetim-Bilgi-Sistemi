using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Messages;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly ISender _sender;

    public MessagesController(ISender sender) => _sender = sender;

    [HttpGet("directory")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MessageUserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MessageUserDto>>>> Directory(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetMessageDirectoryQuery(search), ct);
        return Ok(ApiResponse<IReadOnlyList<MessageUserDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("conversations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConversationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConversationDto>>>> Conversations(
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetConversationsQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<ConversationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("unread-count")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UnreadCount(CancellationToken ct)
    {
        var count = await _sender.Send(new GetUnreadMessageCountQuery(), ct);
        return Ok(ApiResponse<object>.Ok(new { count }, HttpContext.TraceIdentifier));
    }

    [HttpGet("with/{userId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DirectMessageDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DirectMessageDto>>>> Thread(
        Guid userId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetConversationMessagesQuery(userId), ct);
        return Ok(ApiResponse<IReadOnlyList<DirectMessageDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DirectMessageDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<DirectMessageDto>>> Send(
        [FromBody] SendDirectMessageCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<DirectMessageDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("with-file")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DirectMessageDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<DirectMessageDto>>> SendWithFile(
        [FromForm] Guid recipientUserId,
        [FromForm] string? body,
        IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<DirectMessageDto>.Fail(
                new ApiError
                {
                    Code = "VALIDATION",
                    Message = "PDF veya görsel seçin."
                },
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        var result = await _sender.Send(
            new SendDirectMessageCommand
            {
                RecipientUserId = recipientUserId,
                Body = body ?? "",
                Attachment = stream,
                AttachmentFileName = file.FileName,
                AttachmentContentType = file.ContentType
            },
            ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<DirectMessageDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/file")]
    [Authorize]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var file = await _sender.Send(new OpenMessageAttachmentQuery(id), ct);
        return File(file.Stream, file.ContentType, file.DownloadFileName);
    }
}
