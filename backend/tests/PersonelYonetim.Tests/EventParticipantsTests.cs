using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Features.Dashboard;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Dashboard;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class EventParticipantsTests
{
    private sealed class User(bool events = true) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string code) => code == PermissionCodes.EmployeesViewAllUnits || code == PermissionCodes.DashboardView || (events && (code == PermissionCodes.EventsManage || code == PermissionCodes.EventsView));
    }

    private static MemoryStream Workbook(Action<IXLWorksheet> write)
    {
        using var wb = new XLWorkbook();
        write(wb.AddWorksheet("İsimler"));
        var stream = new MemoryStream(); wb.SaveAs(stream); stream.Position = 0; return stream;
    }

    [Fact]
    public async Task Preview_preserves_Turkish_names_order_and_duplicate_people()
    {
        using var file = Workbook(ws => { ws.Cell(1, 1).Value = "Ad Soyad"; ws.Cell(2, 1).Value = "  Şule Öztürk  "; ws.Cell(4, 1).Value = "Şule Öztürk"; ws.Cell(5, 1).Value = "İbrahim Çay"; });
        var result = await new PreviewEventParticipantsHandler(new User()).Handle(new(file, "isimler.xlsx"), default);
        Assert.Equal(new[] { "Şule Öztürk", "Şule Öztürk", "İbrahim Çay" }, result.Names);
        Assert.Equal(1, result.DuplicateNameCount);
    }

    [Fact]
    public async Task Preview_accepts_separate_name_columns_and_ignores_unrequested_data()
    {
        using var file = Workbook(ws => { ws.Cell(1, 1).Value = "Sıra"; ws.Cell(1, 2).Value = "Adı"; ws.Cell(1, 3).Value = "Soyadı"; ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = "İpek"; ws.Cell(2, 3).Value = "Işık"; });
        var result = await new PreviewEventParticipantsHandler(new User()).Handle(new(file, "isimler.XLSX"), default);
        Assert.Equal("İpek Işık", Assert.Single(result.Names));
    }

    [Theory]
    [InlineData("header")]
    [InlineData("empty")]
    [InlineData("numeric")]
    [InlineData("formula")]
    [InlineData("long")]
    [InlineData("missingSurname")]
    [InlineData("duplicateHeader")]
    public async Task Invalid_workbooks_are_rejected_in_full(string kind)
    {
        using var file = Workbook(ws => {
            ws.Cell(1, 1).Value = kind == "header" ? "Telefon" : "Ad Soyad";
            if (kind != "empty") ws.Cell(2, 1).Value = "Geçerli İsim";
            if (kind == "numeric") ws.Cell(3, 1).Value = 123;
            if (kind == "formula") ws.Cell(3, 1).FormulaA1 = "1+1";
            if (kind == "long") ws.Cell(3, 1).Value = new string('a', 201);
            if (kind == "missingSurname") { ws.Cell(1, 1).Value = "Ad"; ws.Cell(1, 2).Value = "Soyad"; }
            if (kind == "duplicateHeader") ws.Cell(1, 2).Value = "Ad Soyad";
        });
        await Assert.ThrowsAsync<ValidationException>(() => new PreviewEventParticipantsHandler(new User()).Handle(new(file, "test.xlsx"), default));
    }

    [Fact]
    public async Task Preview_rejects_corrupt_files_and_unauthorized_users()
    {
        using var file = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<ValidationException>(() => new PreviewEventParticipantsHandler(new User()).Handle(new(file, "broken.xlsx"), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new PreviewEventParticipantsHandler(new User(false)).Handle(new(file, "broken.xlsx"), default));
    }

    [Fact]
    public async Task Malformed_workbook_xml_returns_a_validation_error()
    {
        using var file = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open());
            writer.Write("not valid xml");
        }
        file.Position = 0;
        await Assert.ThrowsAsync<ValidationException>(() => new PreviewEventParticipantsHandler(new User()).Handle(new(file, "broken.xlsx"), default));
    }

    [Fact]
    public async Task Save_is_atomic_and_preserves_roster_when_older_clients_omit_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var id = await new CreateEventHandler(db, new User(), null!, cache).Handle(new CreateEventCommand { Title = "Gezi", Category = "trip", StartAtUtc = DateTime.UtcNow, ParticipantNames = ["İpek Işık", "İpek Işık"] }, default);
        var detail = await new GetEventByIdHandler(db, new User()).Handle(new(id), default);
        Assert.Equal(new[] { "İpek Işık", "İpek Işık" }, detail!.ParticipantNames);
        Assert.Null(detail.ActualAttendees);
        var update = new UpdateEventCommand { Id = id, Title = "Düzenlenen gezi", Category = "trip", StartAtUtc = DateTime.UtcNow };
        var handler = new UpdateEventHandler(db, new User(), cache);
        await handler.Handle(update, default);
        Assert.Equal(2, (await new GetEventByIdHandler(db, new User()).Handle(new(id), default))!.ParticipantNames.Count);
        update.ParticipantNames = [];
        await handler.Handle(update, default);
        Assert.Empty((await new GetEventByIdHandler(db, new User()).Handle(new(id), default))!.ParticipantNames);
    }

    [Fact]
    public async Task Projects_are_separate_from_events_and_counts_respect_permissions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        foreach (var category in new string?[] { "trip", "culture", "project", null }) db.Events.Add(new Event { Title = category ?? "Other", Category = category, StartAtUtc = DateTime.UtcNow });
        db.Events.Add(new Event { Title = "Deleted", Category = "project", IsDeleted = true, StartAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var events = new GetEventsHandler(db, new User());
        Assert.Equal(3, (await events.Handle(new(Kind: "event"), default)).Items.Count);
        Assert.Single((await events.Handle(new(Kind: "project"), default)).Items);
        Assert.Single((await events.Handle(new(Category: "trip"), default)).Items);
        var summary = await new GetDashboardSummaryHandler(db, new User()).Handle(new(), default);
        Assert.Equal(3, summary.Cards.TotalEvents); Assert.Equal(1, summary.Cards.TotalProjects);
        var limited = await new GetDashboardSummaryHandler(db, new User(false)).Handle(new(), default);
        Assert.Equal(0, limited.Cards.TotalEvents); Assert.Equal(0, limited.Cards.TotalProjects);
    }

    [Fact]
    public async Task Template_contains_only_header_and_no_sample_person()
    {
        var file = await new GetEventParticipantsFileHandler(null!, new User()).Handle(new(), default);
        using var stream = new MemoryStream(file.Content);
        using var wb = new XLWorkbook(stream);
        Assert.Equal("Ad Soyad", wb.Worksheet(1).Cell(1, 1).GetString());
        Assert.Equal(1, wb.Worksheet(1).LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void Save_validation_rejects_invalid_names_and_oversized_rosters()
    {
        var command = new CreateEventCommand { Title = "Gezi", StartAtUtc = DateTime.UtcNow, ParticipantNames = ["   "] };
        Assert.False(new CreateEventCommandValidator().Validate(command).IsValid);
        command.ParticipantNames = [new string('a', 201)];
        Assert.False(new CreateEventCommandValidator().Validate(command).IsValid);
        command.ParticipantNames = Enumerable.Repeat("Valid Name", 5001).ToList();
        Assert.False(new CreateEventCommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_database_providers_have_matching_additive_migrations(bool postgres)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (postgres) options.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", x => x.MigrationsAssembly("PersonelYonetim.PostgresMigrations"));
        else options.UseSqlServer("Server=localhost;Database=test;User Id=test;Password=test;TrustServerCertificate=True");
        using var db = new AppDbContext(options.Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript(toMigration: "20261029195500_AddEventParticipantNames");
        Assert.Contains("ParticipantNamesJson", script);
    }
}
