using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Hassas kişisel veriler — ayrı tablo + alan bazlı yetki.
/// TCKN burada tutulur; normal listelerde dönülmez.
/// </summary>
public class EmployeeSensitiveData : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// Data Protection ile şifrelenmiş TCKN (geri açılabilir — yetkili görüntüleme için).
    /// Düz metin tutulmaz.
    /// </summary>
    public string? NationalIdEncrypted { get; set; }

    /// <summary>
    /// Tekillik / arama için tek yönlü hash. Şifreden bağımsız; decrypt etmeden “bu TCKN var mı?” sorulur.
    /// </summary>
    public string? NationalIdHash { get; set; }
}
