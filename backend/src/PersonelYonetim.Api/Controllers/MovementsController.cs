using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Movements;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Kurum geneli görev / hareket geçmişi listesi.
/// </summary>
[ApiController]
[Route("api/movements")]
public sealed class MovementsController : ControllerBase
{
    private readonly ISender _sender;

    public MovementsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.MovementsView)]
    [ProducesResponseType(typeof(ApiResponse<MovementListResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MovementListResultDto>>> GetList(
        [FromQuery] string? search,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? facilityId,
        [FromQuery] MovementType? movementType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMovementListQuery
            {
                Search = search,
                UnitId = unitId,
                FacilityId = facilityId,
                MovementType = movementType,
                From = from,
                To = to,
                Take = take
            },
            cancellationToken);
        return Ok(ApiResponse<MovementListResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("excel")]
    [RequirePermission(PermissionCodes.MovementsView)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? search,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? facilityId,
        [FromQuery] MovementType? movementType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var file = await _sender.Send(
            new ExportMovementListExcelQuery
            {
                Search = search,
                UnitId = unitId,
                FacilityId = facilityId,
                MovementType = movementType,
                From = from,
                To = to
            },
            cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
