using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Kullanıcıya özel uygulama içi bildirim (inbox).
/// AuditLog = "kim ne yaptı" (geçmiş); Notification = "sana haber" (okunacak mesaj).
/// </summary>
public class UserNotification : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Mantıksal grup: System, DataQuality, Security, Export…</summary>
    public string Category { get; set; } = "System";

    /// <summary>UI’da tıklanınca gidilecek göreli yol (örn. /data-quality).</summary>
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
