using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Nested: /api/employees/{employeeId}/notes
/// Listeleme detay DTO içinde görünürlük filtresiyle yapılır.
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/notes")]
public sealed class EmployeeNotesController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeeNotesController(ISender sender) => _sender = sender;

    [HttpGet("~/api/employees/note-form-options")]
    [RequirePermission(PermissionCodes.NotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<NoteFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NoteFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetNoteFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<NoteFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.NotesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        Guid employeeId,
        [FromBody] UpsertNoteRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateNoteCommand
        {
            EmployeeId = employeeId,
            Title = body.Title,
            Category = body.Category,
            Content = body.Content,
            Visibility = body.Visibility,
            ReminderDate = body.ReminderDate
        };

        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{noteId:guid}")]
    [RequirePermission(PermissionCodes.NotesCreate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid employeeId,
        Guid noteId,
        [FromBody] UpsertNoteRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateNoteCommand
        {
            EmployeeId = employeeId,
            NoteId = noteId,
            Title = body.Title,
            Category = body.Category,
            Content = body.Content,
            Visibility = body.Visibility,
            ReminderDate = body.ReminderDate
        };

        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{noteId:guid}")]
    [RequirePermission(PermissionCodes.NotesCreate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid employeeId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteNoteCommand(employeeId, noteId), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
