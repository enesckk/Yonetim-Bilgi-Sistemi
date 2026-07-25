using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Import;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Excel toplu personel aktarımı.
/// GET şablon indirir; POST satır satır işler ve sonuç raporu döner.
/// </summary>
[ApiController]
[Route("api/import/employees")]
public sealed class EmployeeImportController : ControllerBase
{
    private readonly IEmployeeImportService _importService;

    public EmployeeImportController(IEmployeeImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("template")]
    [RequirePermission(PermissionCodes.ImportExcel)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var file = await _importService.BuildTemplateAsync(cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ImportExcel)]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeImportResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeImportResultDto>>> Import(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("file", "Excel dosyası seçilmedi.");

        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(stream, file.FileName, cancellationToken);
        return Ok(ApiResponse<EmployeeImportResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
