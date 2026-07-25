namespace PersonelYonetim.Domain.Common;

/// <summary>
/// Tüm varlıkların ortak kimlik alanı.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
