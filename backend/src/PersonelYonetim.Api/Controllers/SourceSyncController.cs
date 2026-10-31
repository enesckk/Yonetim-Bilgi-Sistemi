using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.SourceSync;
namespace PersonelYonetim.Api.Controllers;
[ApiController]
[Route("api/source-sync")]
[RequirePermission(PermissionCodes.EventsManage)]
[RequirePermission(PermissionCodes.OrganizationManage)]
[RequirePermission(PermissionCodes.EmployeesViewAllUnits)]
[RequestSizeLimit(4 * 1024 * 1024)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SourceSyncController(SourceSyncService service) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] SourceSyncInput input, CancellationToken ct)
        => Ok(ApiResponse<SourceSyncPreview>.Ok(await service.PreviewAsync(input, ct)));
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] SourceSyncApply input, CancellationToken ct)
        => Ok(ApiResponse<SourceSyncPreview>.Ok(await service.ApplyAsync(input, ct)));
}
