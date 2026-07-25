using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Notifications;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Kullanıcının kendi bildirim kutusu.
/// Başkasının bildirimini göremezsin — UserId her zaman oturumdan gelir.
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.NotificationsView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? category = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyNotificationsQuery
            {
                UnreadOnly = unreadOnly,
                Category = category,
                Take = take
            },
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("unread-count")]
    [RequirePermission(PermissionCodes.NotificationsView)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var count = await _sender.Send(new GetUnreadNotificationCountQuery(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { count }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/read")]
    [RequirePermission(PermissionCodes.NotificationsView)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new MarkNotificationReadCommand { Id = id }, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPost("read-all")]
    [RequirePermission(PermissionCodes.NotificationsView)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> MarkAllRead(CancellationToken cancellationToken)
    {
        await _sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Eksik veri, sertifika, tesis kadro vb. taramayı elle çalıştırır.
    /// </summary>
    [HttpPost("scan")]
    [RequirePermission(PermissionCodes.NotificationsView)]
    [ProducesResponseType(typeof(ApiResponse<NotificationScanResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NotificationScanResult>>> Scan(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RunNotificationScanCommand(), cancellationToken);
        return Ok(ApiResponse<NotificationScanResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
