using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/assignments
/// JobTitle (resmi unvan) ≠ JobDuty (fiili görev) — kurumsal HR ayrımı.
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/assignments")]
public sealed class EmployeeAssignmentsController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeAssignmentsController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/assignment-form-options")]
    [RequirePermission(PermissionCodes.AssignmentsManage)]
    [ProducesResponseType(typeof(ApiResponse<AssignmentFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AssignmentFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAssignmentFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<AssignmentFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.AssignmentsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertAssignmentRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateAssignmentCommand
        {
            EmployeeId = employeeId,
            JobDutyId = body.JobDutyId,
            IsPrimary = body.IsPrimary,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            Description = body.Description
        };
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{assignmentId:guid}")]
    [RequirePermission(PermissionCodes.AssignmentsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid assignmentId,
        [FromBody] UpsertAssignmentRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAssignmentCommand
        {
            EmployeeId = employeeId,
            AssignmentId = assignmentId,
            JobDutyId = body.JobDutyId,
            IsPrimary = body.IsPrimary,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            Description = body.Description
        };
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{assignmentId:guid}")]
    [RequirePermission(PermissionCodes.AssignmentsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteAssignmentCommand(employeeId, assignmentId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
