using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Kritik işlem geçmişi — normal kullanıcı silemez.
/// </summary>
public class AuditLog : BaseEntity
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
