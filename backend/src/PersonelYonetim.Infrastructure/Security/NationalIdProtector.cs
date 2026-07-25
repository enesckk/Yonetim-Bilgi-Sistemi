using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PersonelYonetim.Application.Common.Interfaces;

namespace PersonelYonetim.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Data Protection — uygulama anahtarları ile şifreleme.
/// Purpose string farklı amaçları birbirinden ayırır (TCKN ≠ dosya adı vb.).
/// </summary>
public sealed class NationalIdProtector : INationalIdProtector
{
    private const string Purpose = "PersonelYonetim.NationalId.v1";
    private readonly IDataProtector _protector;
    private readonly string _pepper;

    public NationalIdProtector(
        IDataProtectionProvider provider,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _protector = provider.CreateProtector(Purpose);
        _pepper = configuration["Security:NationalIdHashPepper"]?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_pepper))
        {
            throw new InvalidOperationException(
                "Security:NationalIdHashPepper yapılandırılmamış. User Secrets veya ortam değişkeni kullanın.");
        }

        if (!environment.IsDevelopment() &&
            _pepper.StartsWith("LOCAL_DEV_ONLY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Security:NationalIdHashPepper Production ortamında LOCAL_DEV_ONLY ile başlayamaz.");
        }
    }

    public string? Protect(string? digitsOnly)
    {
        if (string.IsNullOrWhiteSpace(digitsOnly))
            return null;

        var digits = DigitsOnly(digitsOnly);
        if (digits.Length != 11)
            return null;

        return _protector.Protect(digits);
    }

    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        // Eski seed / migration öncesi düz metin
        if (LooksLikePlainDigits(stored))
            return DigitsOnly(stored);

        try
        {
            return _protector.Unprotect(stored);
        }
        catch (CryptographicException)
        {
            // Anahtar değişti veya bozuk veri
            return null;
        }
    }

    public string? ComputeLookupHash(string? digitsOnly)
    {
        if (string.IsNullOrWhiteSpace(digitsOnly))
            return null;

        var digits = DigitsOnly(digitsOnly);
        if (digits.Length != 11)
            return null;

        var payload = Encoding.UTF8.GetBytes(digits + "|" + _pepper);
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
    }

    public string Mask(string digitsOnly)
    {
        var digits = DigitsOnly(digitsOnly);
        if (digits.Length >= 4)
            return $"*******{digits[^4..]}";
        return "***********";
    }

    public bool LooksLikePlainDigits(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return false;
        var digits = DigitsOnly(stored);
        return digits.Length == 11 && digits == stored.Trim();
    }

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());
}
