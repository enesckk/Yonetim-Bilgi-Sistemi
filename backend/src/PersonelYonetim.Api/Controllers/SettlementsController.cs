using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/settlements")]
public sealed class SettlementsController : ControllerBase
{
    private readonly ISender _sender;

    public SettlementsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SettlementLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SettlementLookupDto>>>> List(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetSettlementLookupsQuery(search), ct);
        return Ok(ApiResponse<IReadOnlyList<SettlementLookupDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{key}")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<SettlementSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SettlementSummaryDto>>> GetByKey(
        string key,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetSettlementDetailQuery(key), ct);
        return Ok(ApiResponse<SettlementSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}/population")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UpsertPopulation(
        Guid id,
        [FromBody] UpsertSettlementPopulationCommand command,
        CancellationToken ct)
    {
        command.SettlementId = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}/profile")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UpdateProfile(
        Guid id,
        [FromBody] UpdateSettlementProfileCommand command,
        CancellationToken ct)
    {
        command.SettlementId = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/schools")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> UpsertSchool(
        Guid id,
        [FromBody] UpsertSettlementSchoolCommand command,
        CancellationToken ct)
    {
        command.SettlementId = id;
        var schoolId = await _sender.Send(command, ct);
        return Ok(ApiResponse<Guid>.Ok(schoolId, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}/schools/{schoolId:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DeleteSchool(Guid id, Guid schoolId, CancellationToken ct)
    {
        await _sender.Send(new DeleteSettlementSchoolCommand { SettlementId = id, SchoolId = schoolId }, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/areas")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> UpsertArea(
        Guid id,
        [FromBody] UpsertSettlementAreaCommand command,
        CancellationToken ct)
    {
        command.SettlementId = id;
        var areaId = await _sender.Send(command, ct);
        return Ok(ApiResponse<Guid>.Ok(areaId, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}/areas/{areaId:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DeleteArea(Guid id, Guid areaId, CancellationToken ct)
    {
        await _sender.Send(new DeleteSettlementAreaCommand { SettlementId = id, AreaId = areaId }, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
