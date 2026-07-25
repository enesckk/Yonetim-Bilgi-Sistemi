using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Dashboard;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender) => _sender = sender;

    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.DashboardView)]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetSummary(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDashboardSummaryQuery(), cancellationToken);
        return Ok(ApiResponse<DashboardSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
