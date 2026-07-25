using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.DataQuality;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Veri kalitesi — eksik / düşük tamamlanma personel listesi.
/// Raporlar "kaç kişi var?" der; burası "kimde ne eksik?" der.
/// </summary>
[ApiController]
[Route("api/data-quality")]
public sealed class DataQualityController : ControllerBase
{
    private readonly ISender _sender;

    public DataQualityController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.DataQualityView)]
    [ProducesResponseType(typeof(ApiResponse<DataQualityReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DataQualityReportDto>>> GetReport(
        [FromQuery] EmployeeStatus? status,
        [FromQuery] string? issueCode,
        [FromQuery] string? kind,
        [FromQuery] string? completionBand,
        [FromQuery] byte? maxCompletionPercent,
        [FromQuery] bool allStatuses = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetDataQualityReportQuery
            {
                Status = allStatuses ? null : (status ?? EmployeeStatus.Active),
                IssueCode = issueCode,
                Kind = kind,
                CompletionBand = completionBand,
                MaxCompletionPercent = maxCompletionPercent
            },
            cancellationToken);

        return Ok(ApiResponse<DataQualityReportDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
