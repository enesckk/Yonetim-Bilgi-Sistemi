using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>
/// Diğer özelliklerden bildirim üretmek için.
/// Örn. yeni personel, export, veri kalitesi taraması.
/// </summary>
public interface IUserNotificationService
{
    Task NotifyAsync(
        Guid userId,
        string title,
        string body,
        NotificationSeverity severity = NotificationSeverity.Info,
        string category = "System",
        string? linkUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen yetkiye sahip aktif kullanıcılara bildirim gönderir.
    /// </summary>
    Task NotifyUsersWithPermissionAsync(
        string permissionCode,
        string title,
        string body,
        NotificationSeverity severity = NotificationSeverity.Info,
        string category = "System",
        string? linkUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aynı category + linkUrl ile son <paramref name="dedupeDays"/> günde bildirim varsa tekrar eklemez.
    /// Periyodik tarama uyarıları için.
    /// </summary>
    Task NotifyUsersWithPermissionUniqueAsync(
        string permissionCode,
        string title,
        string body,
        string category,
        string linkUrl,
        NotificationSeverity severity = NotificationSeverity.Warning,
        int dedupeDays = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>Periyodik / elle tetiklenen bildirim taraması.</summary>
public interface INotificationScanService
{
    Task<NotificationScanResult> ScanAsync(CancellationToken cancellationToken = default);
}

public sealed class NotificationScanResult
{
    public int CreatedCount { get; init; }
    public int SkippedDuplicateCount { get; init; }
    public IReadOnlyList<string> Summary { get; init; } = [];
}
