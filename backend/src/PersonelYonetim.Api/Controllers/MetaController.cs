using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Statik katalog uçları. Runtime atamalar için /api/roles/matrix kullanın —
/// AuthService DB'deki RolePermissions'ı okur; RolePermissionMatrix yalnızca seed şablonudur.
/// </summary>
[ApiController]
[Route("api/meta")]
[Authorize]
public class MetaController : ControllerBase
{
    [HttpGet("roles")]
    [RequirePermission(PermissionCodes.RolesManage)]
    public ActionResult<ApiResponse<object>> GetRoles()
    {
        // Seed şablonu (dokümantasyon). Canlı matris: GET /api/roles/matrix
        var data = RoleCodes.All.Select(r => new
        {
            r.Code,
            r.Name,
            r.Description,
            seedPermissions = RolePermissionMatrix.GetPermissionsForRole(r.Code)
        });

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }

    [HttpGet("permissions")]
    [RequirePermission(PermissionCodes.RolesManage)]
    public ActionResult<ApiResponse<object>> GetPermissions()
    {
        var data = PermissionCatalog.All.GroupBy(p => p.GroupName)
            .Select(g => new
            {
                group = g.Key,
                items = g.Select(p => new { p.Code, p.Name, p.Description })
            });

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }
}
