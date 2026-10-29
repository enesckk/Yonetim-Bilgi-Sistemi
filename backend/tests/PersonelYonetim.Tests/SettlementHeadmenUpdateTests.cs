using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class SettlementHeadmenUpdateTests
{
    [Fact]
    public async Task Applies_all_120_names_once_without_touching_geometry_or_other_records()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var entries = SettlementHeadmenUpdate.ReadEntries();
        Assert.Equal(120, entries.Count);
        foreach (var entry in entries)
            db.Settlements.Add(new Settlement { OfficialCode = entry.OfficialCode, Name = "SOURCE", GeometryJson = "unchanged", HeadmanPhone = "existing phone", CentroidLat = 37 });
        var other = new Settlement { OfficialCode = "other", Name = "Other", HeadmanName = "Keep" };
        db.Settlements.Add(other);
        await db.SaveChangesAsync();
        await SettlementHeadmenUpdate.SeedAsync(db);
        foreach (var entry in entries)
        {
            var row = await db.Settlements.SingleAsync(x => x.OfficialCode == entry.OfficialCode);
            Assert.Equal(entry.Name, row.DisplayName);
            Assert.Equal(entry.HeadmanName, row.HeadmanName);
            Assert.Equal("SOURCE", row.Name);
            Assert.Equal("unchanged", row.GeometryJson);
            Assert.Equal("existing phone", row.HeadmanPhone);
            Assert.Equal(37d, row.CentroidLat);
        }
        Assert.Equal("Keep", other.HeadmanName);
        var first = await db.Settlements.FirstAsync(x => x.OfficialCode == entries[0].OfficialCode);
        first.HeadmanName = "Later edit";
        await db.SaveChangesAsync();
        await SettlementHeadmenUpdate.SeedAsync(db);
        Assert.Equal("Later edit", first.HeadmanName);
    }

    [Fact]
    public async Task Missing_codes_prevent_partial_import()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SettlementHeadmenUpdate.SeedAsync(db));
        Assert.Empty(await db.AppSettings.ToListAsync());
    }
}
