using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Map;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class SettlementCatalog120UpdateTests
{
    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string code) => true;
    }

    [Fact]
    public async Task Lists_exactly_the_120_user_names_and_headmen_and_preserves_all_149_geometries_and_links()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var entries = SettlementHeadmenUpdate.ReadEntries();
        var extras = SettlementCatalog120Update.ReadLegacyExtraCodes();
        foreach (var code in entries.Select(e => e.OfficialCode).Concat(extras))
            db.Settlements.Add(new Settlement { OfficialCode = code, Name = "RAW-" + code, DisplayName = "Old name", HeadmanName = "Old headman", HeadmanPhone = "existing phone", GeometryJson = "unchanged", CentroidLat = 37, CentroidLng = 38 });
        await db.SaveChangesAsync();
        var extra = await db.Settlements.SingleAsync(s => s.OfficialCode == extras[0]);
        var ev = new Event { Title = "Existing historical event", StartAtUtc = DateTime.UtcNow };
        db.Events.Add(ev);
        db.EventSettlements.Add(new EventSettlement { EventId = ev.Id, SettlementId = extra.Id, AttendanceCount = 10, Notes = "Existing note" });
        await db.SaveChangesAsync();
        await SettlementCatalog120Update.SeedAsync(db);
        Assert.Equal(149, await db.Settlements.CountAsync());
        Assert.Equal(120, await db.Settlements.CountAsync(s => s.IsActive));
        Assert.Equal(29, await db.Settlements.CountAsync(s => !s.IsActive));
        Assert.Equal(10, (await db.EventSettlements.SingleAsync()).AttendanceCount);
        Assert.Equal("Existing note", (await db.EventSettlements.SingleAsync()).Notes);
        Assert.All(await db.Settlements.ToListAsync(), s => {
            Assert.Equal("unchanged", s.GeometryJson); Assert.Equal(37, s.CentroidLat); Assert.Equal(38, s.CentroidLng);
            Assert.Equal("existing phone", s.HeadmanPhone); Assert.StartsWith("RAW-", s.Name);
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var lookups = await new GetSettlementLookupsHandler(db, new User(), cache).Handle(new GetSettlementLookupsQuery(), default);
        var summaries = await new GetSettlementSummariesHandler(db, new User(), cache).Handle(new GetSettlementSummariesQuery(), default);
        Assert.Equal(120, lookups.Count); Assert.Equal(120, summaries.Items.Count);
        foreach (var entry in entries)
        {
            var lookup = Assert.Single(lookups, s => s.OfficialCode == entry.OfficialCode);
            var summary = Assert.Single(summaries.Items, s => s.OfficialCode == entry.OfficialCode);
            Assert.Equal(entry.Name, lookup.Name); Assert.Equal(entry.HeadmanName, lookup.HeadmanName);
            Assert.Equal(entry.Name, summary.Name); Assert.Equal(entry.HeadmanName, summary.HeadmanName);
        }
        Assert.DoesNotContain(lookups, s => extras.Contains(s.OfficialCode));
        var first = await db.Settlements.SingleAsync(s => s.OfficialCode == entries[0].OfficialCode);
        first.HeadmanName = "Later user edit"; await db.SaveChangesAsync();
        await SettlementCatalog120Update.SeedAsync(db);
        Assert.Equal("Later user edit", (await db.Settlements.SingleAsync(s => s.Id == first.Id)).HeadmanName);
        Assert.Equal(1, await db.AppSettings.CountAsync(s => s.Key == SettlementCatalog120Update.Marker));
    }

    [Fact]
    public async Task Missing_reference_causes_no_partial_update_or_receipt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var first = SettlementHeadmenUpdate.ReadEntries()[0];
        db.Settlements.Add(new Settlement { OfficialCode = first.OfficialCode, Name = "Keep", HeadmanName = "Keep" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SettlementCatalog120Update.SeedAsync(db));
        db.ChangeTracker.Clear();
        var unchanged = await db.Settlements.SingleAsync();
        Assert.Equal("Keep", unchanged.HeadmanName); Assert.True(unchanged.IsActive);
        Assert.Empty(await db.AppSettings.ToListAsync());
    }
}
