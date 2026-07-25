using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/special-conditions
/// Belge: multipart upload — JSON body değil.
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/special-conditions")]
public sealed class EmployeeSpecialConditionsController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeSpecialConditionsController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/special-condition-form-options")]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [ProducesResponseType(typeof(ApiResponse<SpecialConditionFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SpecialConditionFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSpecialConditionFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<SpecialConditionFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertSpecialConditionRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateSpecialConditionCommand
        {
            EmployeeId = employeeId,
            ConditionType = body.ConditionType,
            Description = body.Description,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            IsPermanent = body.IsPermanent,
            RequiresDutyAdjustment = body.RequiresDutyAdjustment,
            RequiresWorkspaceAdjustment = body.RequiresWorkspaceAdjustment
        };
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{conditionId:guid}")]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid conditionId,
        [FromBody] UpsertSpecialConditionRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSpecialConditionCommand
        {
            EmployeeId = employeeId,
            ConditionId = conditionId,
            ConditionType = body.ConditionType,
            Description = body.Description,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            IsPermanent = body.IsPermanent,
            RequiresDutyAdjustment = body.RequiresDutyAdjustment,
            RequiresWorkspaceAdjustment = body.RequiresWorkspaceAdjustment
        };
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{conditionId:guid}")]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid conditionId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteSpecialConditionCommand(employeeId, conditionId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// multipart/form-data: field adı "file".
    /// ManageSpecialConditions + (serviste) Files.Upload.
    /// </summary>
    [HttpPost("{conditionId:guid}/document")]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UploadDocument(
        Guid employeeId,
        Guid conditionId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new PersonelYonetim.Application.Common.Exceptions.ValidationException(
                "file",
                "Dosya seçilmedi.");

        await using var stream = file.OpenReadStream();
        await _sender.Send(
            new UploadSpecialConditionDocumentCommand(
                employeeId, conditionId, stream, file.FileName, file.ContentType),
            cancellationToken);

        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{conditionId:guid}/document")]
    [RequirePermission(PermissionCodes.EmployeesManageSpecialConditions)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> RemoveDocument(
        Guid employeeId,
        Guid conditionId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            new RemoveSpecialConditionDocumentCommand(employeeId, conditionId),
            cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Ham dosya döner (ApiResponse sarmalayıcısı yok) — tarayıcı indirmesi için.
    /// ViewSpecialConditions + Files.View serviste kontrol edilir.
    /// </summary>
    [HttpGet("{conditionId:guid}/document")]
    [RequirePermission(PermissionCodes.EmployeesViewSpecialConditions)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDocument(
        Guid employeeId,
        Guid conditionId,
        CancellationToken cancellationToken)
    {
        var doc = await _sender.Send(
            new OpenSpecialConditionDocumentQuery(employeeId, conditionId),
            cancellationToken);
        // FileStreamResult stream'i dispose eder
        return File(doc.Stream, doc.ContentType, doc.DownloadFileName);
    }
}
