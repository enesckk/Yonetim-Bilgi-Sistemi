using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class HistoricalEvents2024And2025ImportTests
{
    private static async Task Prepare(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        foreach (var row in HistoricalEvents2024And2025Import.ReadRows().DistinctBy(r => r.OfficialCode))
            db.Settlements.Add(new Settlement { Name = row.Settlement, DisplayName = row.Settlement, OfficialCode = row.OfficialCode });
        db.OrganizationUnits.Add(new OrganizationUnit { Code = "DTSS", Name = "Devlet Tiyatroları Şehitkamil Sahnesi", Type = OrganizationUnitType.Facility, Status = OrganizationUnitStatus.Active });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Imports_all_473_source_rows_with_exact_dates_notes_counts_locations_and_nine_year_groups()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        await HistoricalEvents2024And2025Import.SeedAsync(db, NullLogger.Instance);
        var events = await db.Events.Include(e => e.Settlements).ThenInclude(s => s.Settlement).ToListAsync();
        Assert.Equal(473, events.Count); Assert.Equal(27076, events.Sum(e => e.ActualAttendees));
        Assert.Equal(27076, events.SelectMany(e => e.Settlements).Sum(s => s.AttendanceCount));
        Assert.Equal(9, events.Select(e => e.SeriesId).Distinct().Count());
        foreach (var row in HistoricalEvents2024And2025Import.ReadRows())
        {
            var ev = Assert.Single(events, e => e.Id == HistoricalEvents2024And2025Import.EventId(row));
            Assert.Equal(row.Title, ev.Title); Assert.Equal(row.Description, ev.Description);
            Assert.Equal(HistoricalEvents2024And2025Import.StartUtc(row), ev.StartAtUtc);
            Assert.True(ev.IsDateOnly); Assert.Null(ev.EndAtUtc); Assert.Null(ev.ExpectedAttendees);
            Assert.Null(ev.ResponsibleEmployeeId); Assert.Null(ev.ParticipantNamesJson);
            Assert.Equal(EventStatus.Completed, ev.Status);
            Assert.Equal(HistoricalEvents2024And2025Import.IsTheatre(row) ? EventCategories.Culture : EventCategories.Trip, ev.Category);
            Assert.Equal(HistoricalEvents2024And2025Import.IsTheatre(row), ev.FacilityId.HasValue);
            Assert.Equal(HistoricalEvents2024And2025Import.ProgramId(row), ev.SeriesId);
            var link = Assert.Single(ev.Settlements);
            Assert.Equal(row.OfficialCode, link.Settlement.OfficialCode);
            Assert.Equal(row.Count, link.AttendanceCount); Assert.Equal(row.Description, link.Notes);
            Assert.Null(link.UniqueBeneficiaryCount);
        }
        foreach (var program in HistoricalEvents2024And2025Import.Expected)
        {
            var sample = HistoricalEvents2024And2025Import.ReadRows().First(r => r.Program == program.Key);
            var group = events.Where(e => e.SeriesId == HistoricalEvents2024And2025Import.ProgramId(sample)).ToList();
            Assert.Equal(program.Value.Records, group.Count); Assert.Equal(program.Value.Attendance, group.Sum(e => e.ActualAttendees));
        }
        // The two schools from 29 Ekim on 6 November are separate events.
        Assert.Equal(2, events.Count(e => e.StartAtUtc == new DateTime(2024, 11, 5, 21, 0, 0, DateTimeKind.Utc)
            && e.Settlements.Any(s => s.Settlement.Name == "29 Ekim")));
        var first = events[0]; first.Description = "Later user edit"; await db.SaveChangesAsync();
        await HistoricalEvents2024And2025Import.SeedAsync(db, NullLogger.Instance);
        Assert.Equal(473, await db.Events.CountAsync());
        Assert.Equal("Later user edit", (await db.Events.SingleAsync(e => e.Id == first.Id)).Description);
        Assert.Equal(473, await db.AuditLogs.CountAsync(x => x.EntityName == "Event"));
    }

    [Fact]
    public async Task Reuses_existing_source_record_and_does_not_touch_previous_2026_import()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        var row = HistoricalEvents2024And2025Import.ReadRows()[0];
        var old = await Existing(db, row, row.Count);
        var unrelated = new Event { Title = "Çocuk Tiyatrosu", Description = "Previous 2026 record", StartAtUtc = new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc), SeriesId = ChildTheatre2026Import.ProgramId };
        db.Events.Add(unrelated); await db.SaveChangesAsync();
        await HistoricalEvents2024And2025Import.SeedAsync(db, NullLogger.Instance);
        Assert.Equal(474, await db.Events.CountAsync()); Assert.True(await db.Events.AnyAsync(e => e.Id == old.Id));
        Assert.Equal("Previous 2026 record", (await db.Events.SingleAsync(e => e.Id == unrelated.Id)).Description);
        Assert.Equal(472, await db.AuditLogs.CountAsync(x => x.EntityName == "Event"));
    }

    [Fact]
    public async Task Post_save_link_mismatch_rolls_back_entire_batch_and_receipt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        await Existing(db, HistoricalEvents2024And2025Import.ReadRows()[0], 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => HistoricalEvents2024And2025Import.SeedAsync(db, NullLogger.Instance));
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Events.CountAsync()); Assert.Equal(1, await db.EventSettlements.CountAsync());
        Assert.False(await db.AppSettings.AnyAsync(x => x.Key == HistoricalEvents2024And2025Import.Marker));
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Missing_reference_prevents_partial_import()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => HistoricalEvents2024And2025Import.SeedAsync(db, NullLogger.Instance));
        Assert.Empty(await db.Events.ToListAsync()); Assert.Empty(await db.AppSettings.ToListAsync());
    }

    private static async Task<Event> Existing(AppDbContext db, HistoricalEvents2024And2025Import.Row source, int linkedCount)
    {
        var ev = new Event { Title = source.Title, Description = source.Description, StartAtUtc = HistoricalEvents2024And2025Import.StartUtc(source), IsDateOnly = true, Category = EventCategories.Trip, Status = EventStatus.Completed, ActualAttendees = source.Count };
        db.Events.Add(ev);
        db.EventSettlements.Add(new EventSettlement { EventId = ev.Id, SettlementId = (await db.Settlements.SingleAsync(s => s.OfficialCode == source.OfficialCode)).Id, AttendanceCount = linkedCount });
        await db.SaveChangesAsync(); return ev;
    }
}
