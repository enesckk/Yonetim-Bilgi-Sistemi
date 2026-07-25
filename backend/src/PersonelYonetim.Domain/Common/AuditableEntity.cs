namespace PersonelYonetim.Domain.Common;

/// <summary>
/// Kim ne zaman oluşturdu / güncelledi bilgisi.
/// Soft-delete: kayıt silinmez, IsDeleted = true yapılır.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
