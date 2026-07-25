namespace PersonelYonetim.Application.Common.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<LoginResultDto> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    SessionPolicyDto GetSessionPolicy();
}

public sealed class LoginRequestDto
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResultDto
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAtUtc { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshTokenExpiresAtUtc { get; init; }
    public required CurrentUserDto User { get; init; }
    public required SessionPolicyDto SessionPolicy { get; init; }
}

public sealed class SessionPolicyDto
{
    public int IdleTimeoutMinutes { get; init; }
    public int AccessTokenMinutes { get; init; }
    public int AbsoluteSessionDays { get; init; }
}

public sealed class CurrentUserDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    /// <summary>Bağlı personel kaydı (varsa).</summary>
    public Guid? EmployeeId { get; init; }
    /// <summary>Bağlı personelin fotoğrafı yüklü mü.</summary>
    public bool HasPhoto { get; init; }
    /// <summary>Rol kodları (SYSTEM_ADMIN vb.) — yetki/JWT.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
    /// <summary>Kullanıcıya gösterilecek rol adları (Sistem Yöneticisi vb.).</summary>
    public IReadOnlyList<string> RoleNames { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
