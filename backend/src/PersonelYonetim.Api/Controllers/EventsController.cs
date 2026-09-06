using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly ISender _sender;

    public EventsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<EventListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EventListDto>>> List(
        [FromQuery] string? search,
        [FromQuery] EventStatus? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetEventsQuery(search, status, fromUtc, toUtc), ct);
        return Ok(ApiResponse<EventListDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("conflicts")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<EventConflictsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EventConflictsDto>>> CheckConflicts(
        [FromQuery] Guid? facilityId,
        [FromQuery] DateTime startAtUtc,
        [FromQuery] DateTime? endAtUtc,
        [FromQuery] Guid? excludeEventId,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new CheckEventConflictsQuery(facilityId, startAtUtc, endAtUtc, excludeEventId),
            ct);
        return Ok(ApiResponse<EventConflictsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("facility-stats")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<EventFacilityStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EventFacilityStatsDto>>> FacilityStats(CancellationToken ct)
    {
        var result = await _sender.Send(new GetEventFacilityStatsQuery(), ct);
        return Ok(ApiResponse<EventFacilityStatsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/timeline")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<EventTimelineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EventTimelineDto>>> Timeline(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetEventTimelineQuery(id), ct);
        return Ok(ApiResponse<EventTimelineDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("import/template")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public IActionResult ImportTemplate()
    {
        const string csv =
            "Baslik,Aciklama,Baslangic,Bitis,Durum,TesisKodu,Enlem,Boylam,Adres,BeklenenKatilimci\n" +
            "Ornek Etkinlik,Aciklama,2026-09-15T14:00,2026-09-15T16:00,Taslak,FAC_KKM,,,Onatli Mah.,120\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", "etkinlik-import-sablon.csv");
    }

    [HttpPost("import")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse<EventImportResultDto>), StatusCodes.Status200OK)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<EventImportResultDto>>> Import(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<EventImportResultDto>.Fail(
                new ApiError { Code = "VALIDATION", Message = "Dosya gerekli." },
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        var result = await _sender.Send(
            new ImportEventsCommand { FileStream = stream, FileName = file.FileName },
            ct);
        return Ok(ApiResponse<EventImportResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<EventDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EventDetailDto>>> GetById(Guid id, CancellationToken ct)
    {
        var detail = await _sender.Send(new GetEventByIdQuery(id), ct);
        if (detail is null)
        {
            return NotFound(ApiResponse<EventDetailDto>.Fail(
                new ApiError { Code = "NOT_FOUND", Message = "Etkinlik bulunamadı." },
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<EventDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateEventCommand command,
        CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateEventCommand command,
        CancellationToken ct)
    {
        command.Id = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [ProducesResponseType(typeof(ApiResponse<EventDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EventDetailDto>>> ChangeStatus(
        Guid id,
        [FromBody] ChangeEventStatusBody body,
        CancellationToken ct)
    {
        var detail = await _sender.Send(
            new ChangeEventStatusCommand
            {
                Id = id,
                Status = body.Status,
                AllowConflicts = body.AllowConflicts
            },
            ct);
        return Ok(ApiResponse<EventDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteEventCommand(id), ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}

public sealed class ChangeEventStatusBody
{
    public EventStatus Status { get; set; }
    public bool AllowConflicts { get; set; }
}
