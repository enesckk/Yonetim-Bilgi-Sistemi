using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Tests;
public class ActivityReportTests
{
    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId => null; public string UserName => "report-test"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions; public bool HasPermission(string code) => permissions.Contains(code);
    }
    private static readonly User Director = new(PermissionCodes.EventsView, PermissionCodes.EventsManage, PermissionCodes.EmployeesViewAllUnits);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFo0AAAAASUVORK5CYII=");
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public AppDbContext Db = null!; public ActivityReportService Service = null!;
        public static async Task<Fixture> New()
        {
            var f = new Fixture { Connection = new("Data Source=:memory:") }; await f.Connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Service = new(f.Db, Director); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private static ActivityReportInput Annual => new("2026 Faaliyet Raporu", new(2026, 1, 1), new(2026, 12, 31));
    private static Event E(string title, DateTime start, int? actual = null, int? expected = null) => new() { Title = title, StartAtUtc = start, ActualAttendees = actual, ExpectedAttendees = expected, Description = "Açıklama: " + title, IsDateOnly = true, Status = EventStatus.Completed };
    [Fact]
    public async Task Annual_report_uses_Turkey_date_boundaries_and_actual_attendance_only()
    {
        await using var f = await Fixture.New();
        var first = E("Yılın ilk projesi", new(2025, 12, 31, 21, 0, 0, DateTimeKind.Utc), 12, 999); first.Category = EventCategories.Project;
        var last = E("Son gün", new(2026, 12, 31, 20, 59, 59, DateTimeKind.Utc), null, 300);
        f.Db.Events.AddRange(first,last,E("Önceki yıl",first.StartAtUtc.AddTicks(-1),44),E("Sonraki yıl",last.StartAtUtc.AddSeconds(1),55)); await f.Db.SaveChangesAsync();
        var id = await f.Service.GenerateAsync(Annual); var report = await f.Service.GetAsync(id);
        Assert.Equal(2, report.Report.SourceCount); Assert.Equal(12, report.Report.SourceAttendance);
        Assert.Equal(new[]{"Yılın ilk projesi","Son gün"},report.Document.Sources.Select(s=>s.Title)); Assert.Null(report.Document.Sources[1].Attendance);
        var projects = await f.Service.GetAsync(await f.Service.GenerateAsync(Annual with { Kind = "project" })); Assert.Single(projects.Document.Sources);
        await using var ms = new MemoryStream(await f.Service.ExcelAsync(id)); using var book = new XLWorkbook(ms);
        Assert.Equal("01.01.2026",book.Worksheet(1).Cell(2,2).GetString()); Assert.Equal(12,book.Worksheet(1).Cell(2,7).GetValue<int>()); Assert.True(book.Worksheet(1).Cell(3,7).IsEmpty());
        await Assert.ThrowsAsync<ValidationException>(()=>f.Service.GenerateAsync(Annual with {FromDate=new(2027,1,1)}));
    }
    [Fact]
    public async Task Selected_images_and_information_notes_are_copied_and_survive_source_changes()
    {
        await using var f = await Fixture.New(); var e=E("Bilim etkinliği",new(2026,5,1,7,0,0,DateTimeKind.Utc),20);e.Description=string.Concat(Enumerable.Repeat("Eksiksiz açıklama satırı. ",160));f.Db.Events.Add(e);await f.Db.SaveChangesAsync();
        f.Db.InformationNotes.Add(new InformationNote { EventId=e.Id,Title="Özgün bilgi notu",Body="Bilgi notundaki özel ayrıntı",DateText="01.05.2026" });await f.Db.SaveChangesAsync();
        var media = new EventMediaService(f.Db,Director);await media.SaveAsync(e.Id,"photo",[new("selected.png",Png),new("unselected.png",Png)]);
        var chosen=(await media.ListAsync(e.Id))[0];await media.SelectForReportAsync(e.Id,chosen.Id,true);
        f.Db.InformationNotes.AddRange(new InformationNote { EventId=e.Id,Title="Uzun bilgi notu 1",Body=string.Concat(Enumerable.Repeat("Uzun bilgi notu açıklaması. ",800)) },new InformationNote { EventId=e.Id,Title="Uzun bilgi notu 2",Body=string.Concat(Enumerable.Repeat("İkinci bilgi notu açıklaması. ",800)) }); await f.Db.SaveChangesAsync();
        var id=await f.Service.GenerateAsync(Annual);var r=await f.Service.GetAsync(id);Assert.Single(r.Assets);Assert.Equal(chosen.Id,r.Assets[0].SourceMediaId);
        Assert.Equal(Png,(await f.Service.AssetAsync(id,r.Assets[0].Id)).Data);
        var source=r.Document.Sources.Single();Assert.Contains("Bilgi notundaki özel ayrıntı",source.Description);
        Assert.Equal(source.Description,string.Concat(r.Document.Pages.Where(p=>p.SourceEventId==e.Id).Select(p=>p.Body)));
        await using (var excelStream = new MemoryStream(await f.Service.ExcelAsync(id)))
        {
            using var workbook = new XLWorkbook(excelStream); var sheet = workbook.Worksheet(1); var lastRow = sheet.LastRowUsed()!.RowNumber();
            Assert.True(lastRow > 2); Assert.Equal(source.Description, string.Concat(Enumerable.Range(2,lastRow-1).Select(n => sheet.Cell(n,8).GetString())));
            Assert.Equal(20, sheet.Cell(2,7).GetValue<int>()); Assert.True(sheet.Cell(3,7).IsEmpty());
        }
        Assert.True(r.Document.Pages.Count>2);await media.DeleteAsync(e.Id,chosen.Id);Assert.Equal(Png,(await f.Service.AssetAsync(id,r.Assets[0].Id)).Data);
        var original=await f.Db.Events.SingleAsync();original.Description="Kaynak sonradan değişti";await f.Db.SaveChangesAsync();Assert.Equal(source.Description,(await f.Service.GetAsync(id)).Document.Sources.Single().Description);
    }
    [Fact]
    public async Task Editor_persists_page_order_text_and_images_without_modifying_sources_and_rejects_stale_save()
    {
        await using var f = await Fixture.New();f.Db.Events.Add(E("Kaynak",new(2026,2,1,7,0,0,DateTimeKind.Utc),10));await f.Db.SaveChangesAsync();
        var id=await f.Service.GenerateAsync(Annual);var r=await f.Service.GetAsync(id);var revision=r.Report.Revision;
        var asset=await f.Service.UploadAsync(id,new("manual.png",Png));var first=r.Document.Pages[0];first.Title="Düzenlenmiş başlık";first.Body="Düzenlenmiş metin";first.ImageIds=[asset.Id];first.Layout="single";
        r.Document.Pages.Reverse();r.Document.Sources.Clear();var updated=await f.Service.SaveAsync(id,new(revision,"Yeni rapor başlığı",r.Document));
        Assert.Equal(revision+1,updated.Report.Revision);Assert.Single(updated.Document.Sources);Assert.Equal("Düzenlenmiş başlık",updated.Document.Pages.Last().Title);
        Assert.Equal("Kaynak",(await f.Db.Events.SingleAsync()).Title);Assert.Equal("Açıklama: Kaynak",(await f.Db.Events.SingleAsync()).Description);
        await Assert.ThrowsAsync<ConflictException>(()=>f.Service.SaveAsync(id,new(revision,"Eski kaydet",r.Document)));
        updated.Document.Pages[0].ImageIds=[Guid.NewGuid()];await Assert.ThrowsAsync<ValidationException>(()=>f.Service.SaveAsync(id,new(updated.Report.Revision,"Geçersiz görsel",updated.Document)));
        var second=await f.Service.GenerateAsync(Annual);Assert.NotEqual(id,second);Assert.Equal("Yeni rapor başlığı",(await f.Service.GetAsync(id)).Report.Title);
    }
    [Fact]
    public async Task Access_requires_event_permission_and_directorate_scope_and_write_permission()
    {
        await using var f=await Fixture.New();f.Db.Events.Add(E("Kaynak",new(2026,2,1,7,0,0,DateTimeKind.Utc)));await f.Db.SaveChangesAsync();var id=await f.Service.GenerateAsync(Annual);
        foreach(var user in new[]{new User(PermissionCodes.EmployeesViewAllUnits),new User(PermissionCodes.EventsView,PermissionCodes.EventsManage)})
        {
            var denied=new ActivityReportService(f.Db,user);await Assert.ThrowsAsync<ForbiddenException>(()=>denied.GetAsync(id));await Assert.ThrowsAsync<ForbiddenException>(()=>denied.GenerateAsync(Annual));await Assert.ThrowsAsync<ForbiddenException>(()=>denied.PdfAsync(id));await Assert.ThrowsAsync<ForbiddenException>(()=>denied.AssetAsync(id,Guid.NewGuid()));
        }
        var viewer=new ActivityReportService(f.Db,new User(PermissionCodes.EventsView,PermissionCodes.EmployeesViewAllUnits));Assert.NotNull(await viewer.GetAsync(id));await Assert.ThrowsAsync<ForbiddenException>(()=>viewer.GenerateAsync(Annual));
    }
    [Fact]
    public async Task Record_date_filter_and_automatic_2026_seed_preserve_saved_edits()
    {
        await using var f=await Fixture.New();var e=E("2025 etkinliği 2026 girişi",new(2025,5,1,7,0,0,DateTimeKind.Utc));e.CreatedAtUtc=new(2026,4,1,7,0,0,DateTimeKind.Utc);f.Db.Events.Add(e);
        f.Db.Events.Add(E("2026 etkinliği",new(2026,5,1,7,0,0,DateTimeKind.Utc)));await f.Db.SaveChangesAsync();
        var record=await f.Service.GetAsync(await f.Service.GenerateAsync(Annual with{DateBasis="record"}));Assert.Contains(record.Document.Sources,s=>s.Id==e.Id);
        await ActivityReport2026Seed.SeedAsync(f.Db);var seeded=await f.Service.GetAsync(ActivityReport2026Seed.ReportId);seeded.Document.Pages[0].Body="Kullanıcının düzenlemesi";await f.Service.SaveAsync(seeded.Report.Id,new(seeded.Report.Revision,"Düzenlenmiş rapor",seeded.Document));
        await ActivityReport2026Seed.SeedAsync(f.Db);Assert.Equal("Kullanıcının düzenlemesi",(await f.Service.GetAsync(ActivityReport2026Seed.ReportId)).Document.Pages[0].Body);
    }
    [Fact]
    public async Task Pdf_is_real_paginated_document_with_selected_images_and_Turkish_text()
    {
        await using var f=await Fixture.New();var e=E("Bilim ve kültür etkinliği",new(2026,5,1,7,0,0,DateTimeKind.Utc),400);e.Description=string.Concat(Enumerable.Repeat("Öğrenciler bilim ve sanat etkinliklerine katılmıştır. ",80));f.Db.Events.Add(e);await f.Db.SaveChangesAsync();
        var media=new EventMediaService(f.Db,Director);await media.SaveAsync(e.Id,"photo",[new("photo.png",Png)]);await media.SelectForReportAsync(e.Id,(await media.ListAsync(e.Id)).Single().Id,true);
        var id=await f.Service.GenerateAsync(Annual);var pdf=await f.Service.PdfAsync(id);Assert.True(pdf.Length>3000);Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf,0,5));
        var sample = Environment.GetEnvironmentVariable("YBS_REPORT_PDF_SAMPLE"); if (!string.IsNullOrEmpty(sample)) await File.WriteAllBytesAsync(sample,pdf);
    }
    [Fact]
    public async Task All_imported_2026_theatre_records_and_Bilimfest_are_present_without_double_counting()
    {
        await using var f = await Fixture.New();
        foreach (var row in ChildTheatre2026Import.ReadRows().DistinctBy(r => r.OfficialCode))
            f.Db.Settlements.Add(new Settlement { Name = row.Settlement, DisplayName = row.Settlement, OfficialCode = row.OfficialCode });
        f.Db.OrganizationUnits.AddRange(new OrganizationUnit { Code = "DTSS", Name = "Devlet Tiyatroları Şehitkamil Sahnesi", Type = OrganizationUnitType.Facility }, new OrganizationUnit { Code = "KKM", Name = "Şehitkamil Kültür ve Kongre Merkezi", Type = OrganizationUnitType.Facility });
        await f.Db.SaveChangesAsync();
        await ChildTheatre2026Import.SeedAsync(f.Db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await Bilimfest2026Import.SeedAsync(f.Db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await ActivityReport2026Seed.SeedAsync(f.Db);
        var r = await f.Service.GetAsync(ActivityReport2026Seed.ReportId);
        Assert.Equal(35, r.Report.SourceCount); Assert.Equal(65678, r.Report.SourceAttendance);
        Assert.Equal(34, r.Document.Sources.Count(s => s.Title == "Çocuk Tiyatrosu"));
        foreach (var row in ChildTheatre2026Import.ReadRows()) Assert.Contains(r.Document.Sources, s => s.Description == row.Description && s.Attendance == row.Count && s.Settlements == row.Settlement);
        var project = Assert.Single(r.Document.Sources, s => s.Id == Bilimfest2026Import.ProjectId);
        Assert.Contains("87 farklı bilim atölyesi", project.Description); Assert.Equal(57678, project.Attendance);
        Assert.True(project.EndDateUtc.HasValue); Assert.Contains(r.Document.Pages, page => page.SourceEventId == Bilimfest2026Import.ProjectId && page.Meta.Contains("17.10.2026"));
        Assert.Equal(35, r.Document.RecordsWithoutImages); Assert.Empty(r.Assets);
        var pdf = await f.Service.PdfAsync(r.Report.Id); Assert.True(pdf.Length > 5000); Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf,0,5));
    }

}
