using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Messages;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/admin/messages")]
[Authorize(Roles = RoleCodes.SystemAdmin)]
public sealed class AdminMessagesController(ISender sender) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetAdminConversationsQuery(search, page), ct);
        return Ok(ApiResponse<MessageArchivePage<AdminConversationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("with/{firstUserId:guid}/{secondUserId:guid}")]
    public async Task<IActionResult> Thread(Guid firstUserId, Guid secondUserId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetAdminConversationQuery(firstUserId, secondUserId, page), ct);
        return Ok(ApiResponse<MessageArchivePage<DirectMessageDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> FileAttachment(Guid id, CancellationToken ct)
    {
        var file = await sender.Send(new OpenAdminMessageAttachmentQuery(id), ct);
        return File(file.Stream, file.ContentType, file.DownloadFileName);
    }
}
