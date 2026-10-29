using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Map;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class SettlementCoverageZeroTests
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
    public async Task No_events_is_red_with_zero_rate_when_population_is_known_and_summary_matches_detail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var empty = new Settlement { Name = "Sofalıcı", OfficialCode = "empty" };
        var unknown = new Settlement { Name = "Unknown population", OfficialCode = "unknown" };
        var quarter = new Settlement { Name = "Quarter", OfficialCode = "quarter" };
        var half = new Settlement { Name = "Half", OfficialCode = "half" };
        db.Settlements.AddRange(empty, unknown, quarter, half);
        db.SettlementPopulations.AddRange(new SettlementPopulation { SettlementId = empty.Id, Population = 378, Year = 2025, Source = "test" },
            new SettlementPopulation { SettlementId = quarter.Id, Population = 100, Year = 2025, Source = "test" },
            new SettlementPopulation { SettlementId = half.Id, Population = 100, Year = 2025, Source = "test" });
        foreach (var (settlement, count) in new[] { (quarter, 25), (half, 50) })
        {
            var ev = new Event { Title = "Completed event", StartAtUtc = DateTime.UtcNow, Status = EventStatus.Completed };
            db.Events.Add(ev);
            db.EventSettlements.Add(new EventSettlement { EventId = ev.Id, SettlementId = settlement.Id, AttendanceCount = count });
        }
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var summaries = await new GetSettlementSummariesHandler(db, new User(), cache).Handle(new GetSettlementSummariesQuery(), default);
        var zero = Assert.Single(summaries.Items, s => s.OfficialCode == "empty");
        Assert.Equal(0m, zero.CoverageRate); Assert.Equal("low", zero.CoverageLevel); Assert.Equal(0, zero.ActivityCount);
        var missing = Assert.Single(summaries.Items, s => s.OfficialCode == "unknown");
        Assert.Null(missing.CoverageRate); Assert.Equal("low", missing.CoverageLevel);
        Assert.Equal("medium", Assert.Single(summaries.Items, s => s.OfficialCode == "quarter").CoverageLevel);
        Assert.Equal("good", Assert.Single(summaries.Items, s => s.OfficialCode == "half").CoverageLevel);
        var detail = await new GetSettlementDetailHandler(db, new User()).Handle(new GetSettlementDetailQuery("empty"), default);
        Assert.Equal(0m, detail.CoverageRate); Assert.Equal("low", detail.CoverageLevel);
        Assert.Equal(378, detail.Population); Assert.Equal(0, detail.AttendanceCount);
    }
}
