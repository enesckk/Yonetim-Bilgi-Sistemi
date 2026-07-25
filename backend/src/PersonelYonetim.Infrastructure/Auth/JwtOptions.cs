namespace PersonelYonetim.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "PersonelYonetim";
    public string Audience { get; set; } = "PersonelYonetim.Spa";

    /// <summary>En az 32 karakter; production'da secret manager / env kullanın.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>Kaydırılan (sliding) yenileme jetonu ömrü — her yenilemede uzar, absolute limiti aşamaz.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Oturumun ilk girişten itibaren en fazla kaç gün açık kalacağı (absolute).</summary>
    public int RefreshTokenAbsoluteDays { get; set; } = 7;

    /// <summary>Hareketsizlik (idle) sonrası oturum düşme süresi (dakika).</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;
}
