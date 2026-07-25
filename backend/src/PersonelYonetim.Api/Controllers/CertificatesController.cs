using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Certificates;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Sertifika kataloğu: /api/certificates
/// Görüntüleme Employees.View, yönetim Certificates.Manage ister.
/// </summary>
[ApiController]
[Route("api/certificates")]
public sealed class CertificatesController : ControllerBase
{
    private readonly ISender _sender;

    public CertificatesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<CertificateCatalogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CertificateCatalogDto>>> GetCatalog(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCertificateCatalogQuery(), cancellationToken);
        return Ok(ApiResponse<CertificateCatalogDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<CertificateCatalogDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CertificateCatalogDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _sender.Send(new GetCertificateCatalogDetailQuery(id), cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<CertificateCatalogDetailDto>.Fail(
                new ApiError { Code = "NOT_FOUND", Message = "Sertifika tanımı bulunamadı." },
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<CertificateCatalogDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateCertificateDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateCertificateDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteCertificateDefinitionCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
