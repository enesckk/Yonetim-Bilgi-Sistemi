namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>
/// TCKN koruma katmanı.
///
/// Encrypt (Protect): DB’de düz metin tutulmaz — yetkili kullanıcıya geri açılabilir (reversible).
/// Hash: arama / tekillik için tek yönlü özet — geri açılamaz.
/// Maskeleme: API/UI’da ****1234 göstermek — şifreleme değildir.
/// </summary>
public interface INationalIdProtector
{
    /// <summary>11 haneli rakamları şifreler. Boşsa null.</summary>
    string? Protect(string? digitsOnly);

    /// <summary>
    /// Şifreyi açar. Eski (düz metin) kayıtları da tanır ve döndürür.
    /// Geçersiz/bozuk payload’da null.
    /// </summary>
    string? Unprotect(string? stored);

    /// <summary>Tekillik kontrolü için SHA-256 (pepper’lı). Şifreleme değildir.</summary>
    string? ComputeLookupHash(string? digitsOnly);

    /// <summary>Maskeli gösterim: *******9012 — decrypt gerekmeden Last4 ile de yapılabilir.</summary>
    string Mask(string digitsOnly);

    bool LooksLikePlainDigits(string? stored);
}
