using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Tests;
public sealed class EditableEventCategoryTests
{
    private sealed class User(bool allowed = true, bool all = true) : ICurrentUserService
    {
        public Guid? UserId => null; public string UserName => "category-tests"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string code) => allowed && (code is PermissionCodes.EventsView or PermissionCodes.EventsManage || all && code == PermissionCodes.EmployeesViewAllUnits);
    }
    [Fact]
    public async Task Custom_names_survive_create_edit_read_filter_and_legacy_client_updates()
    {
        await using var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync(); await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options); await db.Database.EnsureCreatedAsync(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var user = new User(); var create = new CreateEventHandler(db, user, null!, cache); var update = new UpdateEventHandler(db, user, cache);
        var id = await create.Handle(new() { Title = "Bilim projesi", Category = "project", CategoryName = "  TÜBİTAK   4007  ", StartAtUtc = new(2026, 5, 1) }, default);
        var detail = await new GetEventByIdHandler(db, user).Handle(new(id), default); Assert.Equal("project", detail!.Category); Assert.Equal("TÜBİTAK 4007", detail.CategoryName); Assert.Equal("TÜBİTAK 4007", detail.CategoryLabel);
        await update.Handle(new() { Id = id, Title = detail.Title, Category = "project", StartAtUtc = detail.StartAtUtc, Status = EventStatus.Draft }, default);
        Assert.Equal("TÜBİTAK 4007", (await new GetEventByIdHandler(db, user).Handle(new(id), default))!.CategoryName);
        await update.Handle(new() { Id = id, Title = detail.Title, Category = "project", CategoryName = "AB Projeleri", StartAtUtc = detail.StartAtUtc, Status = EventStatus.Draft }, default);
        var filtered = await new GetEventsHandler(db, user).Handle(new(Category: "AB Projeleri", Kind: "project"), default); Assert.Single(filtered.Items); Assert.Equal("AB Projeleri", filtered.Items[0].CategoryLabel);
        Assert.Empty((await new GetEventsHandler(db, user).Handle(new(Kind: "event"), default)).Items);
        var options = await new GetEventCategoryOptionsHandler(db, user).Handle(new(), default); Assert.Equal(new[] { "AB Projeleri" }, options.Projects); Assert.Empty(options.Events);
        await update.Handle(new() { Id = id, Title = detail.Title, Category = "project", CategoryName = "", StartAtUtc = detail.StartAtUtc, Status = EventStatus.Draft }, default);
        Assert.Null((await new GetEventByIdHandler(db, user).Handle(new(id), default))!.CategoryName);
        await create.Handle(new() { Title = "Mahalle buluşması", Category = "other", CategoryName = "Komşuluk Buluşmaları", StartAtUtc = new(2026, 5, 2) }, default);
        Assert.Equal("Komşuluk Buluşmaları", (await new GetEventsHandler(db, user).Handle(new(Kind: "event"), default)).Items.Single().CategoryLabel);
        await create.Handle(new() { Title = "Teknik ad ile özel kategori", Category = "other", CategoryName = "project", StartAtUtc = new(2026, 5, 3) }, default);
        var reserved = await new GetEventsHandler(db, user).Handle(new(Category: "name:project"), default); Assert.Single(reserved.Items); Assert.Equal("other", reserved.Items[0].Category);
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetEventCategoryOptionsHandler(db, new User(false)).Handle(new(), default));
        Assert.Empty((await new GetEventCategoryOptionsHandler(db, new User(true, false)).Handle(new(), default)).Events);
    }
    [Fact]
    public async Task Custom_project_categories_stay_projects_and_distinct_in_reports_and_recurrences()
    {
        await using var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync(); await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options); await db.Database.EnsureCreatedAsync(); using var cache = new MemoryCache(new MemoryCacheOptions()); var user = new User();
        var create = new CreateEventHandler(db, user, null!, cache);
        var id = await create.Handle(new() { Title = "Proje serisi", Category = "project", CategoryName = "GİZ", StartAtUtc = new(2026, 5, 1), RecurrenceFrequency = EventRecurrenceFrequency.Weekly, RecurrenceOccurrences = 3 }, default);
        Assert.Equal(3, await db.Events.CountAsync(e => e.Category == "project" && e.CategoryName == "GİZ"));
        db.Events.AddRange(new Event { Title = "Aynı proje", Category = "project", CategoryName = "TÜBİTAK", StartAtUtc = new(2026, 6, 1), Status = EventStatus.Completed, ActualAttendees = 12 }, new Event { Title = "Aynı proje", Category = "project", CategoryName = "AB Projeleri", StartAtUtc = new(2026, 6, 2), Status = EventStatus.Completed, ActualAttendees = 20 }); await db.SaveChangesAsync();
        var summary = await new ReportSummaryService(db, user).GetAsync("event", new(2026, 1, 1), new(2026, 12, 31)); Assert.Equal(2, summary.ProjectCount); Assert.Equal(0, summary.EventCount); Assert.Equal(32, summary.Attendance); Assert.Equal(2, summary.Rows.Count);
        var reportService = new ActivityReportService(db, user); var report = await reportService.GetAsync(await reportService.GenerateAsync(new("Proje raporu", new(2026, 1, 1), new(2026, 12, 31), Kind: "project"))); Assert.Contains(report.Document.Sources, s => s.Category == "TÜBİTAK"); Assert.Contains(report.Document.Sources, s => s.Category == "AB Projeleri");
    }
    [Theory]
    [InlineData("Tek\nsatır")]
    [InlineData("Kontrol\u0001")]
    public async Task Validation_rejects_multiline_and_control_characters(string name)
    {
        var command = new CreateEventCommand { Title = "Test", StartAtUtc = DateTime.UtcNow, CategoryName = name }; Assert.False((await new CreateEventCommandValidator().ValidateAsync(command)).IsValid);
        command.CategoryName = new string('a', 121); Assert.False((await new CreateEventCommandValidator().ValidateAsync(command)).IsValid);
    }
}
