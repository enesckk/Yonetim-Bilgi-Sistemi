namespace PersonelYonetim.Domain.Settlements;

public static class SettlementCatalog
{
    public static readonly string[] SchoolTypes =
    [
        "Anaokulu", "İlkokul", "Ortaokul", "Lise", "İmam Hatip", "Diğer"
    ];

    public static readonly string[] AreaTypes =
    [
        "Park", "Meydan", "Spor alanı", "Açık etkinlik alanı", "Çocuk oyun alanı", "Yeşil alan"
    ];

    public static string NormalizeSchoolType(string? value) =>
        Match(value, SchoolTypes, "İlkokul");

    public static string NormalizeAreaType(string? value) =>
        Match(value, AreaTypes, "Park");

    public static bool IsSchoolType(string? value) =>
        SchoolTypes.Any(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsAreaType(string? value) =>
        AreaTypes.Any(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string Match(string? value, IReadOnlyList<string> allowed, string fallback)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return fallback;
        return allowed.FirstOrDefault(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))
               ?? fallback;
    }
}
