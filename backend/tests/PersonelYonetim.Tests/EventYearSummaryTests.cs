using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class EventYearSummaryTests
{
    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "year-test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool HasPermission(string code) => permissions.Contains(code);
    }
    [Fact]
    public async Task Summary_uses_Istanbul_year_actual_counts_and_excludes_deleted_drafts_and_expected_attendance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        Event Row(string name, DateTime start, int? actual = null, EventStatus status = EventStatus.Completed) => new()
        { Title = name, StartAtUtc = start, ActualAttendees = actual, ExpectedAttendees = 99999, Status = status };
        var first = Row("New year project", new(2025, 12, 31, 21, 0, 0, DateTimeKind.Utc), 12); first.Category = EventCategories.Project;
        var last = Row("Last day", new(2026, 12, 31, 20, 59, 59, DateTimeKind.Utc));
        var settlements = Row("Settlements", new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var actual = Row("Explicit actual", settlements.StartAtUtc, 20);
        var zero = Row("Explicit zero", settlements.StartAtUtc, 0);
        var deleted = Row("Deleted", settlements.StartAtUtc, 1000); deleted.IsDeleted = true;
        db.Events.AddRange(first, last, settlements, actual, zero, deleted,
            Row("Previous year", new(2025, 12, 31, 20, 59, 59, DateTimeKind.Utc), 1000),
            Row("Next year", new(2026, 12, 31, 21, 0, 0, DateTimeKind.Utc), 1000),
            Row("Draft", settlements.StartAtUtc, 1000, EventStatus.Draft),
            Row("Cancelled", settlements.StartAtUtc, 1000, EventStatus.Cancelled));
        var district = new Settlement { OfficialCode = "year-test", Name = "Test" }; db.Settlements.Add(district);
        db.EventSettlements.AddRange(
            new EventSettlement { Event = settlements, Settlement = district, AttendanceCount = 30 },
            new EventSettlement { Event = actual, Settlement = district, AttendanceCount = 100 },
            new EventSettlement { Event = zero, Settlement = district, AttendanceCount = 100 });
        await db.SaveChangesAsync();
        var summary = await EventYearStatistics.CalculateAsync(db.Events, 2026, default);
        Assert.Equal(4, summary.CompletedEvents); Assert.Equal(1, summary.CompletedProjects);
        Assert.Equal(62, summary.TotalAttendance); Assert.Equal(4, summary.RecordsWithAttendance);
        var denied = new GetEventYearSummaryHandler(db, new User());
        await Assert.ThrowsAsync<ForbiddenException>(() => denied.Handle(new(2026), default));
        var scoped = await new GetEventYearSummaryHandler(db, new User(PermissionCodes.EventsView)).Handle(new(2026), default);
        Assert.Equal(0, scoped.TotalAttendance); Assert.Equal(0, scoped.CompletedProjects);
        var empty = await EventYearStatistics.CalculateAsync(db.Events, 2024, default);
        Assert.Equal(0, empty.CompletedEvents); Assert.Equal(0, empty.TotalAttendance);
    }
    [Fact]
    public async Task Facility_summary_includes_its_halls_without_counting_other_facilities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var parent = new OrganizationUnit { Code = "Y-PARENT", Name = "Centre", Type = OrganizationUnitType.Facility };
        var hall = new OrganizationUnit { Code = "Y-HALL", Name = "Hall", Type = OrganizationUnitType.Facility, Parent = parent };
        var other = new OrganizationUnit { Code = "Y-OTHER", Name = "Other", Type = OrganizationUnitType.Facility };
        db.OrganizationUnits.AddRange(parent, hall, other);
        db.Events.AddRange(new Event { Title = "Centre", Facility = parent, StartAtUtc = new(2026, 1, 1), Status = EventStatus.Completed, ActualAttendees = 10 },
            new Event { Title = "Hall", Facility = hall, StartAtUtc = new(2026, 1, 1), Status = EventStatus.Completed, ActualAttendees = 20 },
            new Event { Title = "Other", Facility = other, StartAtUtc = new(2026, 1, 1), Status = EventStatus.Completed, ActualAttendees = 1000 });
        await db.SaveChangesAsync();
        var handler = new GetEventYearSummaryHandler(db, new User(PermissionCodes.EventsView, PermissionCodes.EmployeesViewAllUnits));
        var summary = await handler.Handle(new(2026, parent.Id), default);
        Assert.Equal(2, summary.CompletedEvents); Assert.Equal(30, summary.TotalAttendance);
        var empty = await handler.Handle(new(2026, Guid.NewGuid()), default);
        Assert.Equal(0, empty.TotalAttendance);
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new(0), default));
    }

}
