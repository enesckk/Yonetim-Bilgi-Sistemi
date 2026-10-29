using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class ChildTheatre2026ImportTests
{
    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string code) => true;
    }

    private static async Task Prepare(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        foreach (var row in ChildTheatre2026Import.ReadRows().DistinctBy(r => r.OfficialCode))
            db.Settlements.Add(new Settlement { Name = row.Settlement, DisplayName = row.Settlement, OfficialCode = row.OfficialCode });
        db.OrganizationUnits.Add(new OrganizationUnit { Code = "DTSS", Name = "Devlet Tiyatroları Şehitkamil Sahnesi", Type = OrganizationUnitType.Facility, Status = OrganizationUnitStatus.Active });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Imports_exactly_34_records_8000_attendances_29_neighborhoods_and_keeps_every_note()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        await ChildTheatre2026Import.SeedAsync(db, NullLogger.Instance);
        var events = await db.Events.Include(e => e.Settlements).ThenInclude(s => s.Settlement).ToListAsync();
        Assert.Equal(34, events.Count); Assert.Equal(8000, events.Sum(e => e.ActualAttendees));
        Assert.Equal(29, events.SelectMany(e => e.Settlements).Select(s => s.SettlementId).Distinct().Count());
        Assert.Equal(8000, events.SelectMany(e => e.Settlements).Sum(s => s.AttendanceCount));
        foreach (var row in ChildTheatre2026Import.ReadRows())
        {
            var ev = Assert.Single(events.Where(e => e.Description == row.Description));
            Assert.Equal(ChildTheatre2026Import.StartUtc(row), ev.StartAtUtc);
            Assert.True(ev.IsDateOnly); Assert.Null(ev.EndAtUtc); Assert.Null(ev.ExpectedAttendees);
            Assert.Null(ev.ResponsibleEmployeeId); Assert.Null(ev.ParticipantNamesJson);
            Assert.Equal(EventStatus.Completed, ev.Status); Assert.Equal(EventCategories.Culture, ev.Category);
            Assert.Equal(ChildTheatre2026Import.ProgramId, ev.SeriesId);
            var link = Assert.Single(ev.Settlements);
            Assert.Equal(row.OfficialCode, link.Settlement.OfficialCode);
            Assert.Equal(row.Count, link.AttendanceCount); Assert.Equal(row.Description, link.Notes);
            Assert.Null(link.UniqueBeneficiaryCount);
        }
        var list = await new GetEventsHandler(db, new User()).Handle(new GetEventsQuery(Search: "Çocuk Tiyatrosu"), default);
        Assert.Equal(34, list.Items.Count);
        Assert.Equal(8000, list.Items.Sum(e => e.ActualAttendees));
        Assert.All(list.Items, e => { Assert.True(e.IsDateOnly); Assert.Single(e.SettlementNames); });
        var detail = await new GetEventByIdHandler(db, new User()).Handle(new GetEventByIdQuery(list.Items[0].Id), default);
        Assert.True(detail!.IsDateOnly); Assert.Equal(34, detail.SeriesCount); Assert.False(string.IsNullOrEmpty(detail.Description));
        // Two different schools on the same date and neighborhood must not be merged.
        Assert.Equal(2, events.Count(e => e.StartAtUtc == new DateTime(2026, 4, 8, 21, 0, 0, DateTimeKind.Utc) && e.Settlements.Any(s => s.Settlement.Name == "8 Şubat")));
        var first = events[0]; first.Description = "Later user edit"; await db.SaveChangesAsync();
        await ChildTheatre2026Import.SeedAsync(db, NullLogger.Instance);
        Assert.Equal(34, await db.Events.CountAsync());
        Assert.Equal("Later user edit", (await db.Events.SingleAsync(e => e.Id == first.Id)).Description);
        Assert.Equal(34, await db.AuditLogs.CountAsync(x => x.EntityName == "Event"));
    }

    [Fact]
    public async Task Existing_matching_record_is_reused_instead_of_duplicated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        var source = ChildTheatre2026Import.ReadRows()[0];
        var ev = await Existing(db, source, source.Count);
        await ChildTheatre2026Import.SeedAsync(db, NullLogger.Instance);
        Assert.Equal(34, await db.Events.CountAsync()); Assert.True(await db.Events.AnyAsync(e => e.Id == ev.Id));
    }

    [Fact]
    public async Task Failed_post_save_total_rolls_back_all_new_records()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await Prepare(db);
        await Existing(db, ChildTheatre2026Import.ReadRows()[0], 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChildTheatre2026Import.SeedAsync(db, NullLogger.Instance));
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Events.CountAsync()); Assert.Equal(1, await db.EventSettlements.CountAsync());
        Assert.False(await db.AppSettings.AnyAsync(x => x.Key == ChildTheatre2026Import.Marker));
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Missing_reference_prevents_any_partial_import()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChildTheatre2026Import.SeedAsync(db, NullLogger.Instance));
        Assert.Empty(await db.Events.ToListAsync()); Assert.Empty(await db.AppSettings.ToListAsync());
    }

    private static async Task<Event> Existing(AppDbContext db, ChildTheatre2026Import.Row source, int linkedCount)
    {
        var ev = new Event { Title = "Çocuk Tiyatrosu", Description = source.Description, StartAtUtc = ChildTheatre2026Import.StartUtc(source), IsDateOnly = true, Category = EventCategories.Culture, Status = EventStatus.Completed, ActualAttendees = source.Count, FacilityId = (await db.OrganizationUnits.SingleAsync()).Id };
        db.Events.Add(ev);
        db.EventSettlements.Add(new EventSettlement { EventId = ev.Id, SettlementId = (await db.Settlements.SingleAsync(s => s.OfficialCode == source.OfficialCode)).Id, AttendanceCount = linkedCount });
        await db.SaveChangesAsync(); return ev;
    }
}
