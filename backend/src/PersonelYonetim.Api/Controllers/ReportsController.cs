using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Reports;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Raporlar — özet (View) + Excel / PDF export.
/// Export dosyası ApiResponse zarfı değil; ham dosya döner.
/// </summary>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender) => _sender = sender;

    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.ReportsView)]
    [ProducesResponseType(typeof(ApiResponse<ReportsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReportsSummaryDto>>> GetSummary(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReportsSummaryQuery(), cancellationToken);
        return Ok(ApiResponse<ReportsSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>Rapor oluşturucu için seçilebilir sütunlar (yetkiye göre).</summary>
    [HttpGet("columns")]
    [RequirePermission(PermissionCodes.ReportsView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportColumnDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReportColumnDto>>>> GetColumns(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReportColumnsQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportColumnDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>Rapor oluşturucu — seçilen sütun/filtre/gruplama ile tablo üretir.</summary>
    [HttpPost("build")]
    [RequirePermission(PermissionCodes.ReportsView)]
    [ProducesResponseType(typeof(ApiResponse<ReportResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReportResultDto>>> Build(
        [FromBody] BuildReportQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<ReportResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("build/excel")]
    [RequirePermission(PermissionCodes.ReportsExportExcel)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuildExcel(
        [FromBody] BuildReportExcelQuery query,
        CancellationToken cancellationToken)
    {
        var file = await _sender.Send(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("build/pdf")]
    [RequirePermission(PermissionCodes.ReportsExportPdf)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuildPdf(
        [FromBody] BuildReportPdfQuery query,
        CancellationToken cancellationToken)
    {
        var file = await _sender.Send(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("employees/excel")]
    [RequirePermission(PermissionCodes.ReportsExportExcel)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportEmployeesExcel(
        [FromQuery] ExportEmployeesExcelQuery query,
        CancellationToken cancellationToken)
    {
        var file = await _sender.Send(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>PDF — yazdırma / resmi belge. Excel ile aynı filtre; ayrı yetki.</summary>
    [HttpGet("employees/pdf")]
    [RequirePermission(PermissionCodes.ReportsExportPdf)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportEmployeesPdf(
        [FromQuery] ExportEmployeesPdfQuery query,
        CancellationToken cancellationToken)
    {
        var file = await _sender.Send(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
