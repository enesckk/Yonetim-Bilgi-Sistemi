using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Catalogs;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Ortak katalog yönetimi: fiili görev, unvan, istihdam türü, tesis türü.
/// Görüntüleme Employees.View / Organization.View; yazma Catalogs.Manage.
/// </summary>
[ApiController]
[Route("api/catalogs")]
public sealed class CatalogsController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogsController(ISender sender) => _sender = sender;

    [HttpGet("overview")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<CatalogsOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CatalogsOverviewDto>>> Overview(CancellationToken ct)
    {
        var result = await _sender.Send(new GetCatalogsOverviewQuery(), ct);
        return Ok(ApiResponse<CatalogsOverviewDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    // —— Fiili görev ——

    [HttpGet("job-duties")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<ApiResponse<CatalogListDto>>> GetJobDuties(CancellationToken ct) =>
        Ok(ApiResponse<CatalogListDto>.Ok(await _sender.Send(new GetJobDutyCatalogQuery(), ct), HttpContext.TraceIdentifier));

    [HttpPost("job-duties")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse<object>>> CreateJobDuty([FromBody] CreateJobDutyCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("job-duties/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> UpdateJobDuty(Guid id, [FromBody] UpdateJobDutyCommand command, CancellationToken ct)
    {
        command.Id = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("job-duties/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> DeleteJobDuty(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteJobDutyCommand(id), ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    // —— Unvan ——

    [HttpGet("job-titles")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<ApiResponse<CatalogListDto>>> GetJobTitles(CancellationToken ct) =>
        Ok(ApiResponse<CatalogListDto>.Ok(await _sender.Send(new GetJobTitleCatalogQuery(), ct), HttpContext.TraceIdentifier));

    [HttpPost("job-titles")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse<object>>> CreateJobTitle([FromBody] CreateJobTitleCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("job-titles/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> UpdateJobTitle(Guid id, [FromBody] UpdateJobTitleCommand command, CancellationToken ct)
    {
        command.Id = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("job-titles/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> DeleteJobTitle(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteJobTitleCommand(id), ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    // —— İstihdam ——

    [HttpGet("employment-types")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<ApiResponse<CatalogListDto>>> GetEmploymentTypes(CancellationToken ct) =>
        Ok(ApiResponse<CatalogListDto>.Ok(await _sender.Send(new GetEmploymentTypeCatalogQuery(), ct), HttpContext.TraceIdentifier));

    [HttpPost("employment-types")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse<object>>> CreateEmploymentType([FromBody] CreateEmploymentTypeCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("employment-types/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> UpdateEmploymentType(Guid id, [FromBody] UpdateEmploymentTypeCommand command, CancellationToken ct)
    {
        command.Id = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("employment-types/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> DeleteEmploymentType(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteEmploymentTypeCommand(id), ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    // —— Tesis türü ——

    [HttpGet("facility-categories")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<ApiResponse<CatalogListDto>>> GetFacilityCategories(CancellationToken ct) =>
        Ok(ApiResponse<CatalogListDto>.Ok(await _sender.Send(new GetFacilityCategoryCatalogQuery(), ct), HttpContext.TraceIdentifier));

    [HttpPost("facility-categories")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse<object>>> CreateFacilityCategory([FromBody] CreateFacilityCategoryCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("facility-categories/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> UpdateFacilityCategory(Guid id, [FromBody] UpdateFacilityCategoryCommand command, CancellationToken ct)
    {
        command.Id = id;
        await _sender.Send(command, ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("facility-categories/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogsManage)]
    public async Task<ActionResult<ApiResponse>> DeleteFacilityCategory(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteFacilityCategoryCommand(id), ct);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
