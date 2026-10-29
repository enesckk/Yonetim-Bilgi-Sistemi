using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>Applies the user's authoritative 120-neighborhood catalog without deleting legacy geometry or relationships.</summary>
public static class SettlementCatalog120Update
{
    public const string Marker = "Settlements.UserCatalog120.20261030";

    public static IReadOnlyList<string> ReadLegacyExtraCodes()
    {
        using var stream = typeof(SettlementCatalog120Update).Assembly.GetManifestResourceStream(
            "PersonelYonetim.Infrastructure.Persistence.Seed.sehitkamil-legacy-extra-codes-20261030.json")
            ?? throw new InvalidOperationException("Eski harita alanları listesi bulunamadı.");
        return JsonSerializer.Deserialize<List<string>>(stream)!;
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261030, 120)", ct);
            else if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'SettlementCatalog120Update', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000", ct);
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(s => s.Key == Marker, ct)) { await tx.CommitAsync(ct); return; }

            var entries = SettlementHeadmenUpdate.ReadEntries();
            var extraCodes = ReadLegacyExtraCodes();
            var allCodes = entries.Select(e => e.OfficialCode).Concat(extraCodes).ToArray();
            var rows = await db.Settlements.IgnoreQueryFilters().Where(s => allCodes.Contains(s.OfficialCode)).ToDictionaryAsync(s => s.OfficialCode, ct);
            if (entries.Count != 120 || extraCodes.Count != 29 || allCodes.Distinct().Count() != 149
                || rows.Count != 149 || entries.Any(e => rows[e.OfficialCode].IsDeleted))
                throw new InvalidOperationException("120 mahalle kataloğu mevcut harita kodlarıyla tam eşleşmedi; işlem uygulanmadı.");

            foreach (var entry in entries)
            {
                var row = rows[entry.OfficialCode];
                row.DisplayName = entry.Name;
                row.HeadmanName = entry.HeadmanName;
                row.IsActive = true;
                row.UpdatedBy = "user-catalog-120-20261030";
            }
            foreach (var code in extraCodes)
            {
                rows[code].IsActive = false;
                rows[code].UpdatedBy = "user-catalog-120-20261030";
            }
            await db.SaveChangesAsync(ct);
            var saved = await db.Settlements.AsNoTracking().IgnoreQueryFilters().Where(s => allCodes.Contains(s.OfficialCode)).ToDictionaryAsync(s => s.OfficialCode, ct);
            if (saved.Values.Count(s => s.IsActive) != 120 || entries.Any(e => !saved[e.OfficialCode].IsActive
                || saved[e.OfficialCode].DisplayName != e.Name || saved[e.OfficialCode].HeadmanName != e.HeadmanName))
                throw new InvalidOperationException("120 mahalle kataloğu kayıt sonrası doğrulanamadı; işlem geri alındı.");
            db.AppSettings.Add(new AppSetting { Key = Marker, Value = "120 mahalle; 120 muhtar bilgisi; 29 eski alan pasif", GroupName = "Mahalleler", DisplayName = "Kullanıcının 120 mahalle listesi uygulandı", IsReadOnly = true });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
