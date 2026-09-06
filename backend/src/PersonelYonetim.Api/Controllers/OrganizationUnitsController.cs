using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Features.Organization;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// İnce controller: iş mantığı yok, yalnızca MediatR'a mesaj gönderir.
/// Öğrenme: ISender.Send(query/command) → pipeline → handler.
/// </summary>
[ApiController]
[Route("api/organization/units")]
public sealed class OrganizationUnitsController : ControllerBase
{
    private readonly ISender _sender;

    public OrganizationUnitsController(ISender sender) => _sender = sender;

    [HttpGet("tree")]
    [RequirePermission(PermissionCodes.OrganizationView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OrganizationUnitNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrganizationUnitNodeDto>>>> GetTree(
        CancellationToken cancellationToken)
    {
        var tree = await _sender.Send(new GetOrganizationTreeQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrganizationUnitNodeDto>>.Ok(tree, HttpContext.TraceIdentifier));
    }

    [HttpGet("coords-import/template")]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    public IActionResult CoordsImportTemplate()
    {
        const string csv = "TesisKodu,Enlem,Boylam\nFAC_KKM,37.0785,37.3720\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", "tesis-koordinat-sablon.csv");
    }

    [HttpPost("coords-import")]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<FacilityCoordsImportResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FacilityCoordsImportResultDto>>> CoordsImport(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<FacilityCoordsImportResultDto>.Fail(
                new ApiError { Code = "VALIDATION", Message = "Dosya gerekli." },
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        var result = await _sender.Send(
            new ImportFacilityCoordsCommand { FileStream = stream, FileName = file.FileName },
            cancellationToken);
        return Ok(ApiResponse<FacilityCoordsImportResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.OrganizationView)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationUnitDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrganizationUnitDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _sender.Send(new GetOrganizationUnitDetailQuery(id), cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<OrganizationUnitDetailDto>.Fail(
                new ApiError { Code = "NOT_FOUND", Message = "Birim bulunamadı." },
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<OrganizationUnitDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("form-options")]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var options = await _sender.Send(new GetOrganizationFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<OrganizationFormOptionsDto>.Ok(options, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateOrganizationUnitCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateOrganizationUnitCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.OrganizationManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteOrganizationUnitCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
