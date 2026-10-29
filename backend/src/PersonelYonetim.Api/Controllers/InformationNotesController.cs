using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.InformationNotes;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/information-notes")]
[RequirePermission(PermissionCodes.EventsView)]
[RequirePermission(PermissionCodes.EmployeesViewAllUnits)]
public class InformationNotesController(InformationNoteService notes) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? eventId, [FromQuery] string? search, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<InformationNoteDto>>.Ok(await notes.ListAsync(eventId, search, ct)));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(ApiResponse<InformationNoteDto>.Ok(await notes.GetAsync(id, ct)));
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct) { var f = await notes.FileAsync(id, ct); return File(f.Data, f.Type, f.Name); }
    [HttpGet("{id:guid}/excel")]
    public async Task<IActionResult> Excel(Guid id, CancellationToken ct) => File(await notes.ExcelAsync(id, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Bilgi-notu.xlsx");
    [HttpPost("preview")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Preview(IFormFile file, CancellationToken ct)
    {
        try { return Ok(ApiResponse<NoteInput>.Ok(InformationNoteDocuments.ReadWord(await ReadFile(file, ct), file.FileName))); }
        catch (Exception ex) when (ex is ArgumentException or System.Xml.XmlException) { return Invalid(ex.Message); }
    }
    [HttpPost]
    [RequirePermission(PermissionCodes.EventsManage)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Save([FromForm] string metadata, IFormFile? file, CancellationToken ct) => await SaveCore(null, metadata, file, ct);
    [HttpPost("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Update(Guid id, [FromForm] string metadata, IFormFile? file, CancellationToken ct) => await SaveCore(id, metadata, file, ct);
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.EventsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await notes.DeleteAsync(id, ct); return Ok(ApiResponse.Ok()); }
    private async Task<IActionResult> SaveCore(Guid? id, string metadata, IFormFile? file, CancellationToken ct)
    {
        try
        {
            var input = JsonSerializer.Deserialize<NoteInput>(metadata, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Bilgi notu alanları gerekli.");
            var result = await notes.SaveAsync(id, input, file is null ? null : await ReadFile(file, ct), file?.FileName, ct);
            return Ok(ApiResponse<InformationNoteDto>.Ok(result));
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException) { return Invalid(ex.Message); }
    }
    private static async Task<byte[]> ReadFile(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > InformationNoteDocuments.MaxBytes) throw new ArgumentException("Dosya en fazla 5 MB olabilir ve boş olamaz.");
        using var ms = new MemoryStream(); await file.CopyToAsync(ms, ct); return ms.ToArray();
    }
    private BadRequestObjectResult Invalid(string message) => BadRequest(ApiResponse.Fail(new ApiError { Code = "VALIDATION_ERROR", Message = message }));
}
