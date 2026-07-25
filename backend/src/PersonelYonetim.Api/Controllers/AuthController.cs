using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    public const string RefreshCookieName = "py_refresh";

    private readonly IAuthService _authService;
    private readonly IHostEnvironment _env;

    public AuthController(IAuthService authService, IHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    /// <summary>
    /// Giriş: access token body'de (Authorization header için),
    /// refresh token HttpOnly cookie'de (JS okuyamaz → XSS'e karşı daha güvenli).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ip, cancellationToken);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        var response = new LoginResponse
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            User = result.User,
            SessionPolicy = result.SessionPolicy
        };

        return Ok(ApiResponse<LoginResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(refreshToken ?? string.Empty, ip, cancellationToken);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        var response = new LoginResponse
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            User = result.User,
            SessionPolicy = result.SessionPolicy
        };

        return Ok(ApiResponse<LoginResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse>> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        await _authService.LogoutAsync(refreshToken, cancellationToken);
        ClearRefreshCookie();
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized(ApiResponse<CurrentUserDto>.Fail(
                new ApiError { Code = "UNAUTHORIZED", Message = "Oturum bulunamadı." },
                HttpContext.TraceIdentifier));

        return Ok(ApiResponse<CurrentUserDto>.Ok(user, HttpContext.TraceIdentifier));
    }

    [HttpGet("session-policy")]
    [ProducesResponseType(typeof(ApiResponse<SessionPolicyDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SessionPolicyDto>> SessionPolicy()
    {
        return Ok(ApiResponse<SessionPolicyDto>.Ok(_authService.GetSessionPolicy(), HttpContext.TraceIdentifier));
    }

    private void SetRefreshCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc,
            Path = "/api/auth",
            IsEssential = true
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            Path = "/api/auth",
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Lax
        });
    }
}

public sealed class LoginResponse
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAtUtc { get; init; }
    public required CurrentUserDto User { get; init; }
    public required SessionPolicyDto SessionPolicy { get; init; }
}
