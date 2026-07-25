using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Infrastructure.Auth;

namespace PersonelYonetim.Infrastructure.Identity;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(JwtTokenService.PermissionClaimType).Select(c => c.Value).ToArray()
        ?? Array.Empty<string>();

    public bool HasPermission(string permissionCode) =>
        IsAuthenticated && Permissions.Contains(permissionCode);
}
