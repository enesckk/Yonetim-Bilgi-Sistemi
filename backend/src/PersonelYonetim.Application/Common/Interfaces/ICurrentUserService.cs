namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>
/// Oturum açmış kullanıcının yetkilerini sorgular.
/// Auth eklendiğinde Infrastructure bu arayüzü uygular.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionCode);
    IReadOnlyCollection<string> Permissions { get; }
}
