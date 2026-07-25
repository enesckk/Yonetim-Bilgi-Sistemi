using Microsoft.AspNetCore.Authorization;
using PersonelYonetim.Infrastructure.Auth;

namespace PersonelYonetim.Api.Authorization;

/// <summary>
/// Kullanım: [RequirePermission(PermissionCodes.EmployeesView)]
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = PermissionPolicyProvider.Prefix + permission;
    }
}
