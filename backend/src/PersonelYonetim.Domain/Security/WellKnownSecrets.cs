namespace PersonelYonetim.Domain.Security;

/// <summary>
/// Geliştirme ortamına özel sırlar. Production seed/login bunları reddeder.
/// </summary>
public static class WellKnownSecrets
{
    public const string DevelopmentPassword = "ChangeMe!123";

    public static bool IsDevelopmentPassword(string? password) =>
        string.Equals(password?.Trim(), DevelopmentPassword, StringComparison.Ordinal);
}
