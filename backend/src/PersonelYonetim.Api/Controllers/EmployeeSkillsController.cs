using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/skills
/// MediatR: Controller → ISender.Send → ValidationBehavior → Handler
/// (eski: Controller → ISkillCommandService)
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/skills")]
public sealed class EmployeeSkillsController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeSkillsController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/skill-form-options")]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse<SkillFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SkillFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSkillFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<SkillFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertEmployeeSkillRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeSkillCommand
        {
            EmployeeId = employeeId,
            SkillId = body.SkillId,
            Level = body.Level,
            ExperienceDuration = body.ExperienceDuration,
            HasCertificate = body.HasCertificate,
            CertificateDate = body.CertificateDate,
            CertificateIssuer = body.CertificateIssuer,
            Description = body.Description
        };

        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{employeeSkillId:guid}")]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid employeeSkillId,
        [FromBody] UpsertEmployeeSkillRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeSkillCommand
        {
            EmployeeId = employeeId,
            EmployeeSkillId = employeeSkillId,
            SkillId = body.SkillId,
            Level = body.Level,
            ExperienceDuration = body.ExperienceDuration,
            HasCertificate = body.HasCertificate,
            CertificateDate = body.CertificateDate,
            CertificateIssuer = body.CertificateIssuer,
            Description = body.Description
        };

        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{employeeSkillId:guid}")]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid employeeSkillId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            new DeleteEmployeeSkillCommand(employeeId, employeeSkillId),
            cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
