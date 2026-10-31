using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using PersonelYonetim.Api.Controllers;
using PersonelYonetim.Application.Common.Interfaces;

namespace PersonelYonetim.Tests;

public class RememberMeCookieTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_sets_browser_lifetime_from_explicit_choice(bool rememberMe)
    {
        var controller = Create();
        controller.Request.Headers.Cookie = "py_remember=1";
        await controller.Login(new LoginRequestDto { RememberMe = rememberMe }, default);
        AssertCookies(controller, rememberMe);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_keeps_browser_lifetime_choice(bool rememberMe)
    {
        var controller = Create();
        controller.Request.Headers.Cookie = "py_refresh=previous; py_remember=" + (rememberMe ? "1" : "0");
        await controller.Refresh(default);
        AssertCookies(controller, rememberMe);
    }

    [Fact]
    public async Task Logout_deletes_both_cookies()
    {
        var controller = Create();
        await controller.Logout(default);
        var cookies = SetCookieHeaderValue.ParseList(controller.Response.Headers.SetCookie.Select(value => value!).ToArray());
        Assert.Equal(2, cookies.Count);
        Assert.All(cookies, c => Assert.True(c.Expires < DateTimeOffset.UtcNow));
        Assert.Contains(cookies, c => c.Name == AuthController.RefreshCookieName);
        Assert.Contains(cookies, c => c.Name == AuthController.RememberCookieName);
    }

    [Fact]
    public void Remember_me_is_opt_in() => Assert.False(new LoginRequestDto().RememberMe);

    private static void AssertCookies(AuthController controller, bool persistent)
    {
        var cookies = SetCookieHeaderValue.ParseList(controller.Response.Headers.SetCookie.Select(value => value!).ToArray());
        Assert.Equal(2, cookies.Count);
        Assert.All(cookies, cookie =>
        {
            Assert.Equal(persistent, cookie.Expires.HasValue);
            if (persistent) Assert.True(cookie.Expires > DateTimeOffset.UtcNow);
            Assert.True(cookie.HttpOnly);
            Assert.True(cookie.Secure);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
            Assert.Equal("/api/auth", cookie.Path.ToString());
        });
        Assert.Equal(persistent ? "1" : "0", cookies.Single(c => c.Name == AuthController.RememberCookieName).Value.ToString());
    }

    private static AuthController Create() => new(new AuthStub(), new ProductionEnvironment())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class AuthStub : IAuthService
    {
        private static LoginResultDto Result() => new()
        {
            AccessToken = "test-access", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            RefreshToken = "test-refresh", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            User = new CurrentUserDto(), SessionPolicy = new SessionPolicyDto()
        };
        public Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ip, CancellationToken ct = default) => Task.FromResult(Result());
        public Task<LoginResultDto> RefreshAsync(string token, string? ip, CancellationToken ct = default) => Task.FromResult(Result());
        public Task LogoutAsync(string? token, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default) => Task.FromResult<CurrentUserDto?>(null);
        public SessionPolicyDto GetSessionPolicy() => new();
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
