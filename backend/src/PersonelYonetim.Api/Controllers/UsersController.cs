using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Admin;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Kullanıcı yönetimi — RBAC'in "User" ucu.
/// Permission doğrudan kullanıcıya değil role'e bağlıdır.
/// </summary>
[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(PermissionCodes.UsersManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserListItemDto>>>> GetList(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUsersQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserListItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.UsersManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(PermissionCodes.UsersManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        Guid id,
        [FromBody] ResetUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly ISender _sender;

    public RolesController(ISender sender) => _sender = sender;

    /// <summary>DB'deki roller + yetki/kullanıcı sayıları (katalog).</summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.UsersManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleListItemDto>>>> GetList(
        CancellationToken cancellationToken)
    {
        // UsersManage yeterli: rol atamak için liste gerekir. Detay matris RolesManage.
        var result = await _sender.Send(new GetRolesQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleListItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Runtime yetki matrisi (DB). Seed şablonuyla karşılaştırma bilgisi de gelir.
    /// </summary>
    [HttpGet("matrix")]
    [RequirePermission(PermissionCodes.RolesManage)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionMatrixDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RolePermissionMatrixDto>>> GetMatrix(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetRolePermissionMatrixQuery(), cancellationToken);
        return Ok(ApiResponse<RolePermissionMatrixDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>Bir rolün permission paketini tamamen değiştirir (replace).</summary>
    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(PermissionCodes.RolesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        command.RoleId = id;
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>RolePermissionMatrix (kod şablonu) değerlerine döner.</summary>
    [HttpPost("{id:guid}/permissions/reset-to-seed")]
    [RequirePermission(PermissionCodes.RolesManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ResetPermissionsToSeed(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new ResetRolePermissionsToSeedCommand { RoleId = id }, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
