using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.Events;
namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/media")]
[RequirePermission(PermissionCodes.EventsView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventMediaController(EventMediaService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid eventId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<EventMediaDto>>.Ok(await service.ListAsync(eventId, ct), HttpContext.TraceIdentifier));

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> FileContent(Guid eventId, Guid id, CancellationToken ct)
    {
        var f = await service.ReadAsync(eventId, id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(f.Data, f.Type);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EventsManage)]
    [RequestSizeLimit(42 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 42 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid eventId, [FromForm] string kind, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 0 || files.Count > 20 || files.Any(f => f.Length == 0 || f.Length > EventMediaService.MaxBytes) || files.Sum(f => f.Length) > 40 * 1024 * 1024)
            throw new ValidationException("files", "Her dosya en fazla 8 MB, bir yükleme toplam en fazla 40 MB olabilir.");
        var uploads = new List<EventMediaUpload>();
        foreach (var file in files)
        {
            await using var s = file.OpenReadStream(); using var ms = new MemoryStream(); await s.CopyToAsync(ms, ct);
            uploads.Add(new(file.FileName, ms.ToArray()));
        }
        await service.SaveAsync(eventId, kind, uploads, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
    [HttpPatch("{id:guid}/report-selection")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<IActionResult> SelectForReport(Guid eventId, Guid id, [FromBody] ReportImageSelection body, CancellationToken ct)
    {
        await service.SelectForReportAsync(eventId, id, body.Selected, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(eventId, id, ct); return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}

public sealed record ReportImageSelection(bool Selected);
