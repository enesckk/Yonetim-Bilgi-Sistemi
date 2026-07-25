using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Skills;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Yetkinlik kataloğu: /api/skills
/// Görüntüleme Employees.View, yönetim Skills.Manage ister.
/// </summary>
[ApiController]
[Route("api/skills")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISender _sender;

    public SkillsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<SkillCatalogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SkillCatalogDto>>> GetCatalog(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSkillCatalogQuery(), cancellationToken);
        return Ok(ApiResponse<SkillCatalogDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<SkillCatalogDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SkillCatalogDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _sender.Send(new GetSkillCatalogDetailQuery(id), cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<SkillCatalogDetailDto>.Fail(
                new ApiError { Code = "NOT_FOUND", Message = "Yetkinlik bulunamadı." },
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<SkillCatalogDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateSkillCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateSkillCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.SkillsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteSkillCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
