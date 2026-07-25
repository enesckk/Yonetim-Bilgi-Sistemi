using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/movements
/// Zaman çizelgesi — append-heavy; düzeltme için update/delete de var (soft-delete).
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/movements")]
public sealed class EmployeeMovementsController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeMovementsController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/movement-form-options")]
    [RequirePermission(PermissionCodes.MovementsView)]
    [ProducesResponseType(typeof(ApiResponse<MovementFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MovementFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMovementFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<MovementFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.MovementsCreate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] CreateMovementRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateMovementCommand
        {
            EmployeeId = employeeId,
            MovementType = body.MovementType,
            OldUnitId = body.OldUnitId, NewUnitId = body.NewUnitId,
            OldFacilityId = body.OldFacilityId, NewFacilityId = body.NewFacilityId,
            OldJobTitleId = body.OldJobTitleId, NewJobTitleId = body.NewJobTitleId,
            OldJobDutyId = body.OldJobDutyId, NewJobDutyId = body.NewJobDutyId,
            StartDate = body.StartDate, EndDate = body.EndDate,
            Reason = body.Reason, Description = body.Description, ApprovedBy = body.ApprovedBy
        };
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{movementId:guid}")]
    [RequirePermission(PermissionCodes.MovementsCreate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid movementId,
        [FromBody] CreateMovementRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMovementCommand
        {
            EmployeeId = employeeId, MovementId = movementId,
            MovementType = body.MovementType,
            OldUnitId = body.OldUnitId, NewUnitId = body.NewUnitId,
            OldFacilityId = body.OldFacilityId, NewFacilityId = body.NewFacilityId,
            OldJobTitleId = body.OldJobTitleId, NewJobTitleId = body.NewJobTitleId,
            OldJobDutyId = body.OldJobDutyId, NewJobDutyId = body.NewJobDutyId,
            StartDate = body.StartDate, EndDate = body.EndDate,
            Reason = body.Reason, Description = body.Description, ApprovedBy = body.ApprovedBy
        };
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{movementId:guid}")]
    [RequirePermission(PermissionCodes.MovementsCreate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid movementId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteMovementCommand(employeeId, movementId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
