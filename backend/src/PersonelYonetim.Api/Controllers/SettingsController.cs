using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Settings;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Çalışma zamanı iş ayarları (DB).
/// JWT / connection string burada yok — onlar appsettings.json.
/// </summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISender _sender;

    public SettingsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.SettingsManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppSettingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppSettingDto>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAppSettingsQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppSettingDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{key}")]
    [RequirePermission(PermissionCodes.SettingsManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        string key,
        [FromBody] UpdateAppSettingCommand command,
        CancellationToken cancellationToken)
    {
        command.Key = Uri.UnescapeDataString(key);
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
