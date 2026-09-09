using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Features.Geo;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/map")]
public sealed class MapController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IGeocodingService _geocoding;

    public MapController(ISender sender, IGeocodingService geocoding)
    {
        _sender = sender;
        _geocoding = geocoding;
    }

    [HttpGet("pins")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<MapPinsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MapPinsDto>>> GetPins(
        [FromQuery] string? kinds,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetMapPinsQuery(kinds, fromUtc, toUtc), ct);
        return Ok(ApiResponse<MapPinsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("settlements/summary")]
    [RequirePermission(PermissionCodes.EventsView)]
    [ProducesResponseType(typeof(ApiResponse<SettlementSummaryListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SettlementSummaryListDto>>> SettlementSummary(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] EventStatus? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetSettlementSummariesQuery(fromUtc, toUtc, category, status, search), ct);
        return Ok(ApiResponse<SettlementSummaryListDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("geocode")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<GeocodeLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GeocodeLookupDto>>> Geocode(
        [FromQuery] string q,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
        {
            return BadRequest(ApiResponse<GeocodeLookupDto>.Fail(
                new ApiError { Code = "VALIDATION", Message = "Arama en az 3 karakter olmalı." },
                HttpContext.TraceIdentifier));
        }

        var hit = await _geocoding.SearchAsync(q, ct);
        return Ok(ApiResponse<GeocodeLookupDto>.Ok(ToLookup(hit), HttpContext.TraceIdentifier));
    }

    [HttpGet("geocode/suggest")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<GeocodeSuggestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GeocodeSuggestDto>>> Suggest(
        [FromQuery] string q,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
        {
            return Ok(ApiResponse<GeocodeSuggestDto>.Ok(new GeocodeSuggestDto(), HttpContext.TraceIdentifier));
        }

        var hits = await _geocoding.SuggestAsync(q, ct);
        return Ok(ApiResponse<GeocodeSuggestDto>.Ok(
            new GeocodeSuggestDto
            {
                Results = hits.Select(h => new GeocodeResultDto
                {
                    Latitude = h.Latitude,
                    Longitude = h.Longitude,
                    DisplayName = h.DisplayName,
                }).ToArray(),
            },
            HttpContext.TraceIdentifier));
    }

    [HttpGet("reverse")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<GeocodeLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GeocodeLookupDto>>> Reverse(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken ct)
    {
        var hit = await _geocoding.ReverseAsync(lat, lng, ct);
        return Ok(ApiResponse<GeocodeLookupDto>.Ok(ToLookup(hit), HttpContext.TraceIdentifier));
    }

    private static GeocodeLookupDto ToLookup(GeocodeHit? hit) =>
        hit is null
            ? new GeocodeLookupDto { Found = false }
            : new GeocodeLookupDto
            {
                Found = true,
                Result = new GeocodeResultDto
                {
                    Latitude = hit.Latitude,
                    Longitude = hit.Longitude,
                    DisplayName = hit.DisplayName
                }
            };
}
