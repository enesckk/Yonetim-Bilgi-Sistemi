using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Api.Controllers;
[ApiController]
[Route("api/events/report-summaries")]
[RequirePermission(PermissionCodes.EventsView)]
[RequirePermission(PermissionCodes.EmployeesViewAllUnits)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportSummariesController(ReportSummaryService service) : ControllerBase
{
    [HttpGet("{type}")]
    public async Task<IActionResult> Get(string type, DateOnly fromDate, DateOnly toDate, string kind = "all", CancellationToken ct = default) => Ok(ApiResponse<ReportSummary>.Ok(await service.GetAsync(type, fromDate, toDate, kind, ct), HttpContext.TraceIdentifier));
    [HttpGet("{type}/word")]
    public async Task<IActionResult> Word(string type, DateOnly fromDate, DateOnly toDate, string kind = "all", CancellationToken ct = default) => File(await service.WordAsync(type, fromDate, toDate, kind, ct), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{(type == "facility" ? "tesis-raporu" : "etkinlik-proje-raporu")}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.docx");
}
