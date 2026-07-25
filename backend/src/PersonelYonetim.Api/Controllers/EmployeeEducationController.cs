using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/education
/// MediatR: Controller → ISender → ValidationBehavior → Handler
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/education")]
public sealed class EmployeeEducationController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeEducationController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/education-form-options")]
    [RequirePermission(PermissionCodes.EducationManage)]
    [ProducesResponseType(typeof(ApiResponse<EducationFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EducationFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEducationFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<EducationFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EducationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertEducationRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateEducationCommand
        {
            EmployeeId = employeeId,
            Level = body.Level,
            University = body.University,
            Faculty = body.Faculty,
            School = body.School,
            Department = body.Department,
            Program = body.Program,
            GraduationYear = body.GraduationYear,
            CompletionStatus = body.CompletionStatus,
            DiplomaNumber = body.DiplomaNumber,
            Description = body.Description
        };

        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{educationId:guid}")]
    [RequirePermission(PermissionCodes.EducationManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid educationId,
        [FromBody] UpsertEducationRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEducationCommand
        {
            EmployeeId = employeeId,
            EducationId = educationId,
            Level = body.Level,
            University = body.University,
            Faculty = body.Faculty,
            School = body.School,
            Department = body.Department,
            Program = body.Program,
            GraduationYear = body.GraduationYear,
            CompletionStatus = body.CompletionStatus,
            DiplomaNumber = body.DiplomaNumber,
            Description = body.Description
        };

        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{educationId:guid}")]
    [RequirePermission(PermissionCodes.EducationManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid educationId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteEducationCommand(employeeId, educationId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
