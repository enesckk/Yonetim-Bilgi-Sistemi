using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.WorkTasks;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/work-tasks")]
public sealed class WorkTasksController : ControllerBase
{
    private readonly ISender _sender;

    public WorkTasksController(ISender sender) => _sender = sender;

    [HttpGet("options")]
    [RequirePermission(PermissionCodes.TasksView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WorkTaskUserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkTaskUserDto>>>> Options(CancellationToken ct)
    {
        var result = await _sender.Send(new GetWorkTaskOptionsQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<WorkTaskUserDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.TasksView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WorkTaskListRowDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkTaskListRowDto>>>> List(
        [FromQuery] string? tab,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetWorkTasksQuery(tab), ct);
        return Ok(ApiResponse<IReadOnlyList<WorkTaskListRowDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.TasksView)]
    [ProducesResponseType(typeof(ApiResponse<WorkTaskDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkTaskDetailDto>>> Get(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetWorkTaskQuery(id), ct);
        return Ok(ApiResponse<WorkTaskDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<WorkTaskDetailDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<WorkTaskDetailDto>>> Create(
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] WorkTaskKind kind,
        [FromForm] Guid? assigneeUserId,
        [FromForm] Guid? unitId,
        [FromForm] DateTime? dueOn,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        var command = new CreateWorkTaskCommand
        {
            Title = title,
            Description = description ?? "",
            Kind = kind,
            AssigneeUserId = assigneeUserId,
            UnitId = unitId,
            DueOn = dueOn,
            Uploads = ToUploads(files)
        };
        var result = await _sender.Send(command, ct);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<WorkTaskDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<WorkTaskDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkTaskDetailDto>>> Submit(
        Guid id,
        [FromForm] string? note,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new SubmitWorkTaskCommand
            {
                Id = id,
                Note = note,
                Uploads = ToUploads(files)
            },
            ct);
        return Ok(ApiResponse<WorkTaskDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/review")]
    [RequirePermission(PermissionCodes.TasksReview)]
    [ProducesResponseType(typeof(ApiResponse<WorkTaskDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkTaskDetailDto>>> Review(
        Guid id,
        [FromBody] ReviewWorkTaskCommand command,
        CancellationToken ct)
    {
        command.Id = id;
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<WorkTaskDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.TasksView)]
    [ProducesResponseType(typeof(ApiResponse<WorkTaskDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkTaskDetailDto>>> Cancel(
        Guid id,
        [FromBody] CancelWorkTaskCommand? command,
        CancellationToken ct)
    {
        command ??= new CancelWorkTaskCommand();
        command.Id = id;
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<WorkTaskDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.TasksView)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteWorkTaskCommand { Id = id }, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/files/{attachmentId:guid}")]
    [RequirePermission(PermissionCodes.TasksView)]
    public async Task<IActionResult> File(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var file = await _sender.Send(new OpenWorkTaskAttachmentQuery(id, attachmentId), ct);
        return File(file.Stream, file.ContentType, file.DownloadFileName);
    }

    private static List<WorkTaskUpload> ToUploads(List<IFormFile>? files)
    {
        var list = new List<WorkTaskUpload>();
        if (files is null) return list;
        foreach (var file in files.Where(f => f.Length > 0).Take(8))
        {
            var ms = new MemoryStream();
            file.CopyTo(ms);
            ms.Position = 0;
            list.Add(new WorkTaskUpload
            {
                Content = ms,
                FileName = file.FileName,
                ContentType = file.ContentType
            });
        }
        return list;
    }
}
