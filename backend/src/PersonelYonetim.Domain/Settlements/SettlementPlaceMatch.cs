using System.Globalization;

namespace PersonelYonetim.Domain.Settlements;

public static class SettlementPlaceMatch
{
    private static readonly CompareInfo Tr = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public static string Fold(string value) => value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));

    public static bool IsStrong(string query, string name)
    {
        var q = Fold(query);
        var n = Fold(name);
        if (q.Length < 2 || n.Length == 0) return false;
        return n == q || n.StartsWith(q, StringComparison.Ordinal);
    }

    public static bool IsLoose(string query, string name)
    {
        var q = Fold(query);
        var n = Fold(name);
        if (q.Length < 2 || n.Length == 0) return false;
        return Tr.IndexOf(n, q, CompareOptions.IgnoreCase) >= 0;
    }
}
