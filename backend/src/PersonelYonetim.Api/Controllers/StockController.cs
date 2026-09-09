using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController : ControllerBase
{
    private readonly ISender _sender;

    public StockController(ISender sender) => _sender = sender;

    [HttpGet("options")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<StockOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StockOptionsDto>>> Options(CancellationToken ct)
    {
        var result = await _sender.Send(new GetStockOptionsQuery(), ct);
        return Ok(ApiResponse<StockOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<StockSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StockSummaryDto>>> Summary(CancellationToken ct)
    {
        var result = await _sender.Send(new GetStockSummaryQuery(), ct);
        return Ok(ApiResponse<StockSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("items")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockItemListRowDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockItemListRowDto>>>> Items(
        [FromQuery] string? search,
        [FromQuery] StockCategory? category,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetStockItemsQuery(search, category, includeInactive), ct);
        return Ok(ApiResponse<IReadOnlyList<StockItemListRowDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("items/{id:guid}")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<StockItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StockItemDetailDto>>> Item(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetStockItemQuery(id), ct);
        return Ok(ApiResponse<StockItemDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("items")]
    [RequirePermission(PermissionCodes.StockManage)]
    [ProducesResponseType(typeof(ApiResponse<StockItemDetailDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<StockItemDetailDto>>> CreateItem(
        [FromBody] UpsertStockItemCommand command,
        CancellationToken ct)
    {
        command.Id = null;
        var result = await _sender.Send(command, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<StockItemDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("items/{id:guid}")]
    [RequirePermission(PermissionCodes.StockManage)]
    [ProducesResponseType(typeof(ApiResponse<StockItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StockItemDetailDto>>> UpdateItem(
        Guid id,
        [FromBody] UpsertStockItemCommand command,
        CancellationToken ct)
    {
        command.Id = id;
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<StockItemDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("items/{id:guid}")]
    [RequirePermission(PermissionCodes.StockManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ArchiveItem(Guid id, CancellationToken ct)
    {
        await _sender.Send(new ArchiveStockItemCommand(id), ct);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    [HttpGet("locations/{id:guid}")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<StockLocationDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StockLocationDetailDto>>> Location(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetStockLocationQuery(id), ct);
        return Ok(ApiResponse<StockLocationDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("movements")]
    [RequirePermission(PermissionCodes.StockView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockMovementDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockMovementDto>>>> Movements(
        [FromQuery] Guid? itemId,
        [FromQuery] Guid? locationId,
        [FromQuery] int take = 80,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetStockMovementsQuery(itemId, locationId, take), ct);
        return Ok(ApiResponse<IReadOnlyList<StockMovementDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("movements")]
    [RequirePermission(PermissionCodes.StockManage)]
    [ProducesResponseType(typeof(ApiResponse<StockMovementDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<StockMovementDto>>> CreateMovement(
        [FromBody] CreateStockMovementCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<StockMovementDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
