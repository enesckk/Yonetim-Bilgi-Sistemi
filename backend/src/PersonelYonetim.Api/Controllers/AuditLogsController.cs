using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Audit;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// İşlem geçmişi — yazma yok (interceptor yazar); yalnızca AuditLogs.View ile okuma.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly ISender _sender;

    public AuditLogsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.AuditLogsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogListItemDto>>>> GetList(
        [FromQuery] GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditLogListItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
