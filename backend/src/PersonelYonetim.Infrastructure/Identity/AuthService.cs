using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonelYonetim.Application.Common;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Auth;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IJwtTokenService _tokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AuthService(
        AppDbContext db,
        IJwtTokenService tokenService,
        ICurrentUserService currentUser,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResultDto> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("UserName", "Kullanıcı adı ve şifre zorunludur.");

        var userName = request.UserName.Trim();
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Başarısız giriş: bilinmeyen kullanıcı {UserName}", userName);
            WriteAnonymousAuthAudit("LoginFailedUnknownUser", ipAddress, new { userName });
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(ErrorCodes.InvalidCredentials, "Kullanıcı adı veya şifre hatalı.");
        }

        if (user.LockoutEndUtc is not null && user.LockoutEndUtc > DateTime.UtcNow)
        {
            WriteAuthAudit(user, "LoginLocked", ipAddress, new { reason = "lockout_active" });
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(ErrorCodes.AccountLocked, "Hesap geçici olarak kilitli.");
        }

        if (!user.IsActive)
        {
            WriteAuthAudit(user, "LoginDenied", ipAddress, new { reason = "inactive" });
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(ErrorCodes.AccountInactive, "Hesap pasif durumda.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            var locked = false;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                locked = true;
            }

            WriteAuthAudit(user, locked ? "LoginLockout" : "LoginFailed", ipAddress, new
            {
                failedCount = user.FailedLoginCount,
                locked,
                lockoutMinutes = locked ? LockoutDuration.TotalMinutes : (double?)null
            });
            await _db.SaveChangesAsync(cancellationToken);

            if (locked)
                _logger.LogWarning("Hesap kilitlendi: {UserName} ({Minutes} dk)", user.UserName, LockoutDuration.TotalMinutes);
            else
                _logger.LogWarning("Başarısız giriş: {UserName}", user.UserName);

            throw new UnauthorizedAppException(ErrorCodes.InvalidCredentials, "Kullanıcı adı veya şifre hatalı.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;

        var roles = user.UserRoles
            .Where(x => x.Role.Code is not null)
            .Select(x => x.Role.Code!)
            .Distinct()
            .ToList();

        var roleNames = user.UserRoles
            .Select(x => x.Role.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToList();

        var now = DateTime.UtcNow;
        var absoluteExpires = now.AddDays(Math.Max(1, _jwtOptions.RefreshTokenAbsoluteDays));
        var result = await IssueTokensAsync(
            user, roles, roleNames, permissions, ipAddress,
            absoluteExpiresAtUtc: absoluteExpires,
            cancellationToken);
        WriteAuthAudit(user, "LoginSuccess", ipAddress, new { roles });
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<LoginResultDto> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAppException(ErrorCodes.TokenInvalid, "Refresh token gerekli.");

        var hash = _tokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null || !stored.User.IsActive)
            throw new UnauthorizedAppException(ErrorCodes.TokenInvalid, "Refresh token geçersiz veya süresi dolmuş.");

        var now = DateTime.UtcNow;
        if (stored.RevokedAtUtc is not null || now >= stored.ExpiresAtUtc || now >= stored.AbsoluteExpiresAtUtc)
            throw new UnauthorizedAppException(ErrorCodes.TokenInvalid, "Refresh token geçersiz veya süresi dolmuş.");

        var idleMinutes = Math.Max(1, _jwtOptions.IdleTimeoutMinutes);
        if (stored.LastUsedAtUtc.AddMinutes(idleMinutes) < now)
        {
            stored.RevokedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(ErrorCodes.TokenInvalid, "Oturum hareketsizlik nedeniyle sonlandırıldı.");
        }

        // Rotation: eski token iptal, yeni token ver (absolute süre korunur)
        stored.RevokedAtUtc = now;

        var user = stored.User;
        var roles = user.UserRoles
            .Where(x => x.Role.Code is not null)
            .Select(x => x.Role.Code!)
            .Distinct()
            .ToList();
        var roleNames = user.UserRoles
            .Select(x => x.Role.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        var permissions = user.UserRoles
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToList();

        var result = await IssueTokensAsync(
            user, roles, roleNames, permissions, ipAddress,
            absoluteExpiresAtUtc: stored.AbsoluteExpiresAtUtc,
            cancellationToken);
        stored.ReplacedByTokenHash = _tokenService.HashToken(result.RefreshToken);
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = _tokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null)
            return;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is null)
            return null;

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId, cancellationToken);

        if (user is null)
            return null;

        var hasPhoto = false;
        if (user.EmployeeId.HasValue)
        {
            hasPhoto = await _db.Employees.AsNoTracking()
                .AnyAsync(
                    x => x.Id == user.EmployeeId && x.PhotoPath != null && x.PhotoPath != "",
                    cancellationToken);
        }

        return new CurrentUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            EmployeeId = user.EmployeeId,
            HasPhoto = hasPhoto,
            Roles = user.UserRoles.Where(x => x.Role.Code is not null).Select(x => x.Role.Code!).Distinct().ToList(),
            RoleNames = user.UserRoles.Select(x => x.Role.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
            Permissions = _currentUser.Permissions.ToList()
        };
    }

    public SessionPolicyDto GetSessionPolicy() => BuildSessionPolicy();

    /// <summary>Şifre değişimi / pasifleştirmede tüm yenileme jetonlarını iptal eder.</summary>
    public static async Task RevokeAllRefreshTokensAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var t in tokens)
            t.RevokedAtUtc = now;
    }

    private async Task<LoginResultDto> IssueTokensAsync(
        AppUser user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> roleNames,
        IReadOnlyList<string> permissions,
        string? ipAddress,
        DateTime absoluteExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var (accessToken, accessExpires) = _tokenService.CreateAccessToken(
            user.Id,
            user.UserName,
            user.DisplayName,
            roles,
            permissions);

        var now = DateTime.UtcNow;
        var slidingDays = Math.Max(1, _jwtOptions.RefreshTokenDays);
        var slidingExpires = now.AddDays(slidingDays);
        if (slidingExpires > absoluteExpiresAtUtc)
            slidingExpires = absoluteExpiresAtUtc;

        if (slidingExpires <= now)
            throw new UnauthorizedAppException(ErrorCodes.TokenInvalid, "Oturum süresi dolmuş.");

        var refreshToken = _tokenService.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(refreshToken),
            ExpiresAtUtc = slidingExpires,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            LastUsedAtUtc = now,
            CreatedByIp = ipAddress,
            CreatedBy = user.UserName
        });

        var hasPhoto = false;
        if (user.EmployeeId.HasValue)
        {
            hasPhoto = await _db.Employees.AsNoTracking()
                .AnyAsync(
                    x => x.Id == user.EmployeeId && x.PhotoPath != null && x.PhotoPath != "",
                    cancellationToken);
        }

        return new LoginResultDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessExpires,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = slidingExpires,
            SessionPolicy = BuildSessionPolicy(),
            User = new CurrentUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                EmployeeId = user.EmployeeId,
                HasPhoto = hasPhoto,
                Roles = roles,
                RoleNames = roleNames,
                Permissions = permissions
            }
        };
    }

    private SessionPolicyDto BuildSessionPolicy() => new()
    {
        IdleTimeoutMinutes = Math.Max(1, _jwtOptions.IdleTimeoutMinutes),
        AccessTokenMinutes = Math.Max(1, _jwtOptions.AccessTokenMinutes),
        AbsoluteSessionDays = Math.Max(1, _jwtOptions.RefreshTokenAbsoluteDays)
    };

    private void WriteAuthAudit(AppUser user, string action, string? ipAddress, object details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "AppUser",
            EntityId = user.Id.ToString(),
            UserId = user.Id.ToString(),
            UserName = user.UserName,
            IpAddress = ipAddress,
            NewValuesJson = JsonSerializer.Serialize(details)
        });
    }

    private void WriteAnonymousAuthAudit(string action, string? ipAddress, object details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "AppUser",
            EntityId = null,
            UserId = null,
            UserName = "unknown",
            IpAddress = ipAddress,
            NewValuesJson = JsonSerializer.Serialize(details)
        });
    }
}
