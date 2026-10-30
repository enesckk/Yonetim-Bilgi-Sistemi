using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Api.Controllers;
[ApiController]
[Route("api/events/reports")]
[RequirePermission(PermissionCodes.EventsView)]
[RequirePermission(PermissionCodes.EmployeesViewAllUnits)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ActivityReportsController(ActivityReportService service) : ControllerBase
{
    private IActionResult OkData<T>(T value) => Ok(ApiResponse<T>.Ok(value, HttpContext.TraceIdentifier));
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => OkData(await service.ListAsync(ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => OkData(await service.GetAsync(id, ct));
    [HttpPost] [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<IActionResult> Create([FromBody] ActivityReportInput input, CancellationToken ct) => OkData(new { id = await service.GenerateAsync(input, ct) });
    [HttpPut("{id:guid}")] [RequirePermission(PermissionCodes.EventsManage)] [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Save(Guid id, [FromBody] ActivityReportUpdate input, CancellationToken ct) => OkData(await service.SaveAsync(id, input, ct));
    [HttpPost("{id:guid}/assets")] [RequirePermission(PermissionCodes.EventsManage)] [RequestSizeLimit(9 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > EventMediaService.MaxBytes) throw new ValidationException("file", "En fazla 8 MB büyüklüğünde bir görsel seçin.");
        await using var stream = file.OpenReadStream(); using var ms = new MemoryStream(); await stream.CopyToAsync(ms, ct);
        return OkData(await service.UploadAsync(id, new EventMediaUpload(file.FileName, ms.ToArray()), ct));
    }
    [HttpGet("{id:guid}/assets/{assetId:guid}")]
    public async Task<IActionResult> Asset(Guid id, Guid assetId, CancellationToken ct)
    {
        var file = await service.AssetAsync(id, assetId, ct); Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(file.Data, file.Type);
    }
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct) => File(await service.PdfAsync(id, ct), "application/pdf", $"faaliyet-raporu-{id.ToString()[..8]}.pdf");
    [HttpGet("{id:guid}/excel")]
    public async Task<IActionResult> Excel(Guid id, CancellationToken ct) => File(await service.ExcelAsync(id, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"etkinlik-raporu-{id.ToString()[..8]}.xlsx");
}
