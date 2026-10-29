using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>User-supplied names and headmen, applied once without changing boundaries or identifiers.</summary>
public static class SettlementHeadmenUpdate
{
    public sealed record Entry(string OfficialCode, string Name, string HeadmanName);

    public static IReadOnlyList<Entry> ReadEntries()
    {
        using var stream = typeof(SettlementHeadmenUpdate).Assembly.GetManifestResourceStream(
            "PersonelYonetim.Infrastructure.Persistence.Seed.sehitkamil-headmen-20261029.json")
            ?? throw new InvalidOperationException("Mahalle muhtar listesi bulunamadı.");
        return JsonSerializer.Deserialize<List<Entry>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        const string marker = "Settlements.Headmen20261029";
        if (await db.AppSettings.AnyAsync(x => x.Key == marker, ct)) return;
        var entries = ReadEntries();
        var rows = await db.Settlements.IgnoreQueryFilters().ToDictionaryAsync(x => x.OfficialCode, ct);
        // Validate the complete import before making any changes; never create or guess a boundary.
        if (entries.Select(x => x.OfficialCode).Distinct().Count() != entries.Count || entries.Any(x => !rows.ContainsKey(x.OfficialCode)))
            throw new InvalidOperationException("Mahalle muhtar listesi mevcut mahallelerle tam eşleşmedi.");
        foreach (var entry in entries)
        {
            var row = rows[entry.OfficialCode];
            row.DisplayName = entry.Name;
            row.HeadmanName = entry.HeadmanName;
            row.UpdatedBy = "seed-headmen-20261029";
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        db.AppSettings.Add(new AppSetting { Key = marker, Value = "120", GroupName = "Mahalleler", DisplayName = "Kullanıcı mahalle ve muhtar listesi uygulandı", IsReadOnly = true });
        await db.SaveChangesAsync(ct);
    }
}
