using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}/certificates")]
public sealed class EmployeeCertificatesController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeCertificatesController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/certificate-form-options")]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse<CertificateFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CertificateFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCertificateFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<CertificateFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertCertificateRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateCertificateCommand
        {
            EmployeeId = employeeId,
            CertificateDefinitionId = body.CertificateDefinitionId,
            Name = body.Name,
            Issuer = body.Issuer,
            Category = body.Category,
            IssuedOn = body.IssuedOn,
            ExpiresOn = body.ExpiresOn,
            DocumentNumber = body.DocumentNumber,
            Description = body.Description,
            RelatedSkillId = body.RelatedSkillId
        };
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{certificateId:guid}")]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid certificateId,
        [FromBody] UpsertCertificateRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCertificateCommand
        {
            EmployeeId = employeeId,
            CertificateId = certificateId,
            CertificateDefinitionId = body.CertificateDefinitionId,
            Name = body.Name,
            Issuer = body.Issuer,
            Category = body.Category,
            IssuedOn = body.IssuedOn,
            ExpiresOn = body.ExpiresOn,
            DocumentNumber = body.DocumentNumber,
            Description = body.Description,
            RelatedSkillId = body.RelatedSkillId
        };
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{certificateId:guid}")]
    [RequirePermission(PermissionCodes.CertificatesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid certificateId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteCertificateCommand(employeeId, certificateId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
