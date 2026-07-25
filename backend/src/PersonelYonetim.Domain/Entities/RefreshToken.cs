using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Yenileme jetonu — düz metin DB'de tutulmaz, hash saklanır.
/// Access token kısa ömürlüdür; refresh ile yenilenir.
/// </summary>
public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>İlk girişte sabitlenen üst sınır — yenilemede uzatılmaz.</summary>
    public DateTime AbsoluteExpiresAtUtc { get; set; }

    /// <summary>Son başarılı kullanım (login/refresh); idle kontrolü için.</summary>
    public DateTime LastUsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive =>
        RevokedAtUtc is null
        && DateTime.UtcNow < ExpiresAtUtc
        && DateTime.UtcNow < AbsoluteExpiresAtUtc;
}
