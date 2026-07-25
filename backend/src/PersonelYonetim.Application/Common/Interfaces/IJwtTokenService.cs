namespace PersonelYonetim.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        string userName,
        string displayName,
        IEnumerable<string> roles,
        IEnumerable<string> permissions);

    string CreateRefreshToken();
    string HashToken(string token);
}
