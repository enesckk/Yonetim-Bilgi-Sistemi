using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Tests;

public sealed class ReportSummaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Migration_snapshots_match_the_current_model_without_connecting_to_a_database(bool postgres)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (postgres) options.UseNpgsql("Host=localhost;Database=model_check", p => p.MigrationsAssembly("PersonelYonetim.PostgresMigrations"));
        else options.UseSqlServer("Server=localhost;Database=model_check;Integrated Security=True;TrustServerCertificate=True");
        using var db = new AppDbContext(options.Options); Assert.False(db.Database.HasPendingModelChanges());
    }
    private sealed class User(bool allowed) : ICurrentUserService
    {
        public Guid? UserId => null; public string UserName => "report-tests"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => allowed ? [PermissionCodes.EventsView, PermissionCodes.EmployeesViewAllUnits, PermissionCodes.EventsManage] : [PermissionCodes.EventsView];
        public bool HasPermission(string code) => Permissions.Contains(code);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = new("Data Source=:memory:"); public AppDbContext Db = null!;
        public ReportSummaryService Service => new(Db, new User(true));
        public static async Task<Fixture> New()
        {
            var f = new Fixture(); await f.Connection.OpenAsync(); f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    [Fact]
    public async Task Facility_period_keeps_distinct_source_rows_and_excludes_other_months_and_deleted_rows()
    {
        await using var f = await Fixture.New(); var unit = new OrganizationUnit { Name = "Sanat Kütüphanesi", Type = OrganizationUnitType.Facility }; f.Db.OrganizationUnits.Add(unit);
        f.Db.FacilityUsageRecords.AddRange(new FacilityUsageRecord { FacilityId = unit.Id, Year = 2025, Month = 12, ParticipantCount = 100, SourceKey = "a" }, new FacilityUsageRecord { FacilityId = unit.Id, Year = 2025, Month = 12, ParticipantCount = 50, SourceKey = "b" }, new FacilityUsageRecord { FacilityId = unit.Id, Year = 2026, Month = 1, ParticipantCount = 20, SourceKey = "c" }, new FacilityUsageRecord { FacilityId = unit.Id, Year = 2025, Month = 12, ParticipantCount = 999, SourceKey = "deleted", IsDeleted = true }); await f.Db.SaveChangesAsync();
        var r = await f.Service.GetAsync("facility", new(2025, 12, 1), new(2025, 12, 31)); Assert.Equal(150, r.Attendance); Assert.Equal(2, r.RecordCount); Assert.Single(r.Rows); Assert.Equal(150, r.Rows[0].Attendance); Assert.Single(r.Months);
        var empty = await f.Service.GetAsync("facility", new(2024, 1, 1), new(2024, 1, 31)); Assert.Empty(empty.Rows); Assert.Equal(0, empty.RecordCount);
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.GetAsync("facility", new(2025, 12, 2), new(2025, 12, 31)));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ReportSummaryService(f.Db, new User(false)).WordAsync("facility", new(2025, 12, 1), new(2025, 12, 31)));
    }
    [Fact]
    public async Task Event_report_matches_annual_scope_boundaries_and_never_counts_expected_or_cancelled_attendance()
    {
        await using var f = await Fixture.New(); var first = new DateTime(2025, 12, 31, 21, 0, 0, DateTimeKind.Utc);
        var project = new Event { Title = "BilimFest 2026", StartAtUtc = first, Category = EventCategories.Project, ActualAttendees = 57678, Status = EventStatus.Completed };
        var unknown = new Event { Title = "Tiyatro 2026", StartAtUtc = first.AddDays(1), ExpectedAttendees = 999, Status = EventStatus.Completed };
        var known = new Event { Title = "Tiyatro", StartAtUtc = first.AddDays(2), Status = EventStatus.Completed };
        f.Db.Events.AddRange(project, unknown, known, new Event { Title = "Önceki yıl", StartAtUtc = first.AddTicks(-1), ActualAttendees = 50, Status = EventStatus.Completed }, new Event { Title = "İptal", StartAtUtc = first, Status = EventStatus.Cancelled, ActualAttendees = 100 }, new Event { Title = "Taslak", StartAtUtc = first, Status = EventStatus.Draft, ActualAttendees = 200 });
        var settlement = new Settlement { Name = "Test", OfficialCode = "TEST" }; f.Db.Settlements.Add(settlement); f.Db.EventSettlements.Add(new EventSettlement { EventId = known.Id, SettlementId = settlement.Id, AttendanceCount = 200 }); await f.Db.SaveChangesAsync();
        var r = await f.Service.GetAsync("event", new(2026, 1, 1), new(2026, 12, 31)); Assert.Equal(3, r.RecordCount); Assert.Equal(57878, r.Attendance); Assert.Equal(2, r.EventCount); Assert.Equal(1, r.ProjectCount); Assert.Equal(2, r.RecordsWithAttendance); Assert.Equal(2, r.Rows.Single(row => row.Name == "Tiyatro 2026").RecordCount);
        var p = await f.Service.GetAsync("event", new(2026, 1, 1), new(2026, 12, 31), "project"); Assert.Single(p.Rows); Assert.Equal(57678, p.Attendance);
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.GetAsync("bad", new(2026, 1, 1), new(2026, 12, 31)));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.GetAsync("event", new(2027, 1, 1), new(2026, 12, 31)));
    }
    [Fact]
    public async Task Source_import_is_atomic_and_preserves_repeated_month_rows_without_duplicate_reimport()
    {
        await using var f = await Fixture.New(); var unit = new OrganizationUnit { Name = "M. Sami Benli Kütüphanesi", Type = OrganizationUnitType.Facility }; f.Db.OrganizationUnits.Add(unit); await f.Db.SaveChangesAsync();
        var rows = Enumerable.Range(0, 333).Select(i => new SourceFacilityUsage(unit.Name, 2025, 12, i == 332 ? 3821850 - 332 : 1)).ToArray();
        Assert.Equal(333, await FacilityUsageImport.ImportAsync(f.Db, rows)); Assert.Equal(333, await f.Db.FacilityUsageRecords.CountAsync()); Assert.Equal(3821850, await f.Db.FacilityUsageRecords.SumAsync(r => r.ParticipantCount));
        Assert.Equal(0, await FacilityUsageImport.ImportAsync(f.Db, rows.Reverse().ToArray())); Assert.Equal(333, await f.Db.FacilityUsageRecords.CountAsync());
        var row = await f.Db.FacilityUsageRecords.FirstAsync(); row.ParticipantCount = 999; await f.Db.SaveChangesAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => FacilityUsageImport.ImportAsync(f.Db, rows));
        f.Db.ChangeTracker.Clear(); Assert.Equal(999, (await f.Db.FacilityUsageRecords.SingleAsync(r => r.Id == row.Id)).ParticipantCount);
    }
    [Fact]
    public async Task Word_exports_are_valid_packages_and_use_saved_activity_text_images_and_order()
    {
        await using var f = await Fixture.New(); f.Db.Events.Add(new Event { Title = "BilimFest", Description = "Özgün açıklama & özel <metin>", StartAtUtc = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc), ActualAttendees = 57678, Status = EventStatus.Completed }); await f.Db.SaveChangesAsync();
        var service = new ActivityReportService(f.Db, new User(true)); var id = await service.GenerateAsync(new("2026 Faaliyet Raporu", new(2026, 1, 1), new(2026, 12, 31))); var detail = await service.GetAsync(id);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFo0AAAAASUVORK5CYII="); var asset = await service.UploadAsync(id, new("photo.png", png));
        detail.Document.Pages[1].Body = "Kaydedilen Türkçe metin & <ayrıntı>"; detail.Document.Pages[1].ImageIds = [asset.Id]; detail.Document.Pages.Reverse(); await service.SaveAsync(id, new(detail.Report.Revision, detail.Report.Title, detail.Document));
        var word = await service.WordAsync(id); Validate(word); using var zip = new ZipArchive(new MemoryStream(word)); using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); var xml = await reader.ReadToEndAsync(); Assert.Contains("Kaydedilen Türkçe metin", xml); Assert.DoesNotContain("Özgün açıklama", xml); Assert.Contains("&amp;", xml); Assert.Single(zip.Entries, e => e.FullName.StartsWith("word/media/"));
        var summary = new ReportSummary("facility", new(2024, 1, 1), new(2026, 12, 31), 333, 3821850, 333, 0, 0, DateTime.UtcNow, Enumerable.Range(1, 45).Select(n => new ReportSummaryRow(n.ToString(), "Şehitkamil Kültür ve Kongre Merkezi " + n, "Tesis", 12, n * 1000)).ToList(), [new(2026, 1, 12, 80000), new(2026, 2, 10, 70000)]);
        var summaryWord = ReportWordDocument.Summary(summary); Validate(summaryWord);
        var qa = Environment.GetEnvironmentVariable("YBS_REPORT_QA_DIR"); if (!string.IsNullOrEmpty(qa)) { Directory.CreateDirectory(qa); await File.WriteAllBytesAsync(Path.Combine(qa, "activity.docx"), word); await File.WriteAllBytesAsync(Path.Combine(qa, "facility.docx"), summaryWord); await File.WriteAllBytesAsync(Path.Combine(qa, "events.docx"), await f.Service.WordAsync("event", new(2026, 1, 1), new(2026, 12, 31))); }
    }
    private static void Validate(byte[] bytes)
    {
        using var word = WordprocessingDocument.Open(new MemoryStream(bytes), false); var errors = new OpenXmlValidator().Validate(word).ToList(); Assert.True(errors.Count == 0, string.Join("; ", errors.Select(e => e.Description + " at " + e.Path?.XPath)));
    }
}
