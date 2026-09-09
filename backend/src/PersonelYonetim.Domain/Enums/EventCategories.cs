namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Etkinlik türü kodları — ayrı katalog tablosu yok; mevcut Event.Category alanını kullanır.
/// </summary>
public static class EventCategories
{
    public const string Education = "education";
    public const string Health = "health";
    public const string SocialSupport = "social_support";
    public const string Culture = "culture";
    public const string Sports = "sports";
    public const string Youth = "youth";
    public const string Women = "women";
    public const string Elderly = "elderly";
    public const string Other = "other";

    public static readonly (string Code, string Label)[] All =
    [
        (Education, "Eğitim"),
        (Health, "Sağlık"),
        (SocialSupport, "Sosyal Destek"),
        (Culture, "Kültür / Sanat"),
        (Sports, "Spor"),
        (Youth, "Çocuk / Gençlik"),
        (Women, "Kadın"),
        (Elderly, "Yaşlı"),
        (Other, "Diğer")
    ];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Any(x => x.Code == code);

    public static string Label(string? code) =>
        All.FirstOrDefault(x => x.Code == code).Label ?? "Diğer";

    public static string Normalize(string? code) =>
        IsKnown(code) ? code!.Trim() : Other;
}
