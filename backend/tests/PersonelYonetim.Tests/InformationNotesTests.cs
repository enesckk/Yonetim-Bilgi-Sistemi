using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.InformationNotes;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class InformationNotesTests
{
    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool HasPermission(string code) => permissions.Contains(code);
    }
    private static readonly User Director = new(PermissionCodes.EventsView, PermissionCodes.EventsManage, PermissionCodes.EmployeesViewAllUnits);
    private static AppDbContext Context(SqliteConnection connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    private static async Task Prepare(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        db.OrganizationUnits.Add(new OrganizationUnit { Code = "KKM", Name = "Şehitkamil Kültür ve Kongre Merkezi", Type = OrganizationUnitType.Facility });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Template_export_preserves_branding_fixed_fields_and_escapes_multiline_text()
    {
        const string body = "Türkçe ı İ ğ ş & <etiket>\nİkinci satır\n\nSon paragraf";
        var data = InformationNoteDocuments.CreateWord("31 Ekim 2026", "Kültür & sanat", body);
        var parsed = InformationNoteDocuments.ReadWord(data, "note.docx");
        Assert.Equal("31 Ekim 2026", parsed.DateText);
        Assert.Equal("Kültür & sanat", parsed.Subject);
        Assert.Equal("Şehitkamil Belediye Başkanlığı", parsed.Organizer);
        Assert.Equal("Kültür, Sanat ve Sosyal İşler Müdürlüğü", parsed.PreparedBy);
        Assert.Contains("Türkçe ı İ ğ ş & <etiket>", parsed.Body);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(data));
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("word/media/"));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("ŞEHİTKAMİL", xml);
        Assert.Contains("&lt;etiket&gt;", xml);
    }

    [Fact]
    public async Task Imports_one_completed_project_and_exact_original_note_once_without_inventing_neighborhoods_or_names()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Context(connection); await Prepare(db);
        await Bilimfest2026Import.SeedAsync(db, NullLogger.Instance);
        var project = await db.Events.SingleAsync(); var note = await db.InformationNotes.SingleAsync();
        var document = Bilimfest2026Import.ReadDocument(); var source = InformationNoteDocuments.ReadWord(document, "Bilimfest.docx");
        Assert.Equal(EventCategories.Project, project.Category); Assert.Equal(EventStatus.Completed, project.Status);
        Assert.Equal(57678, project.ActualAttendees); Assert.Null(project.ExpectedAttendees); Assert.Null(project.ParticipantNamesJson);
        Assert.Equal(new DateTime(2026, 10, 14, 21, 0, 0, DateTimeKind.Utc), project.StartAtUtc);
        Assert.Equal(new DateTime(2026, 10, 17, 21, 0, 0, DateTimeKind.Utc).AddTicks(-1), project.EndAtUtc);
        Assert.True(project.IsDateOnly); Assert.Equal((await db.OrganizationUnits.SingleAsync()).Id, project.FacilityId);
        Assert.Equal(source.Body, project.Description); Assert.Contains("87 farklı bilim atölyesi", note.Body);
        Assert.Equal(source.Body, note.Body); Assert.Equal(source.DateText, note.DateText); Assert.Equal(source.Subject, note.Subject);
        Assert.Equal(source.Organizer, note.Organizer); Assert.Equal(source.PreparedBy, note.PreparedBy); Assert.Equal(project.Id, note.EventId);
        Assert.Equal(document, note.FileData); Assert.Empty(await db.EventSettlements.ToListAsync());
        var service = new InformationNoteService(db, Director);
        Assert.Single(await service.ListAsync(project.Id, null)); Assert.Empty(await service.ListAsync(Guid.NewGuid(), null));
        Assert.Equal(source.Body, (await service.GetAsync(note.Id)).Body);
        var download = await service.FileAsync(note.Id); Assert.NotEqual(document, download.Data); Assert.Equal(source.Body, InformationNoteDocuments.ReadWord(download.Data, download.Name).Body); Assert.Equal(InformationNoteDocuments.DocxType, download.Type);
        using var excel = new XLWorkbook(new MemoryStream(await service.ExcelAsync(note.Id)));
        Assert.Equal(source.DateText, excel.Worksheet(1).Cell(3, 2).GetString()); Assert.Equal(source.Body, excel.Worksheet(1).Cell(8, 2).GetString());
        Assert.False(excel.Worksheet(1).Cell(8, 2).HasFormula);
        project.Title = "User changed project"; note.Body = "User changed note"; await db.SaveChangesAsync();
        await Bilimfest2026Import.SeedAsync(db, NullLogger.Instance);
        Assert.Single(await db.Events.ToListAsync()); Assert.Single(await db.InformationNotes.ToListAsync());
        Assert.Equal("User changed note", (await db.InformationNotes.SingleAsync()).Body);
    }

    [Fact]
    public async Task Missing_facility_or_conflicting_project_rolls_back_without_a_receipt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Context(connection); await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bilimfest2026Import.SeedAsync(db, NullLogger.Instance));
        Assert.Empty(await db.Events.ToListAsync()); Assert.Empty(await db.InformationNotes.ToListAsync()); Assert.Empty(await db.AppSettings.ToListAsync());
        db.OrganizationUnits.Add(new OrganizationUnit { Code = "KKM", Name = "Venue", Type = OrganizationUnitType.Facility });
        db.Events.Add(new Event { Id = Bilimfest2026Import.ProjectId, Title = "Conflicting", StartAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bilimfest2026Import.SeedAsync(db, NullLogger.Instance));
        Assert.Single(await db.Events.ToListAsync()); Assert.Empty(await db.InformationNotes.ToListAsync()); Assert.Empty(await db.AppSettings.ToListAsync());
    }

    [Fact]
    public async Task Notes_support_validated_create_edit_private_download_and_soft_delete()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Context(connection); await Prepare(db);
        var service = new InformationNoteService(db, Director);
        var input = new NoteInput("New note", null, "15–17 Ekim 2026", "Organizer", "Subject", "Author", "Complete description");
        var note = await service.SaveAsync(null, input, "%PDF-1.7\nTest file"u8.ToArray(), "source.pdf");
        Assert.Equal("Complete description", note.Body); Assert.Null(note.EventId);
        await service.SaveAsync(note.Id, input with { Body = "Edited body" });
        Assert.Equal("Edited body", (await service.GetAsync(note.Id)).Body); Assert.EndsWith(".docx", (await service.FileAsync(note.Id)).Name); Assert.Equal("Edited body", InformationNoteDocuments.ReadWord((await service.FileAsync(note.Id)).Data, "note.docx").Body);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, input with { EventId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, input with { Body = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, input, "wrong"u8.ToArray(), "wrong.docx"));
        Assert.Single(await db.InformationNotes.ToListAsync());
        var officer = new InformationNoteService(db, new User(PermissionCodes.EmployeesViewAllUnits));
        await Assert.ThrowsAsync<ForbiddenException>(() => officer.ListAsync(null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => officer.FileAsync(note.Id));
        var scoped = new InformationNoteService(db, new User(PermissionCodes.EventsView, PermissionCodes.EventsManage));
        await Assert.ThrowsAsync<ForbiddenException>(() => scoped.GetAsync(note.Id));
        var viewer = new InformationNoteService(db, new User(PermissionCodes.EventsView, PermissionCodes.EmployeesViewAllUnits));
        Assert.Single(await viewer.ListAsync(null, null)); await Assert.ThrowsAsync<ForbiddenException>(() => viewer.DeleteAsync(note.Id));
        var original = Bilimfest2026Import.ReadDocument();
        await service.SaveAsync(note.Id, input, original, "original.docx");
        Assert.Equal(input.Body, InformationNoteDocuments.ReadWord((await service.FileAsync(note.Id)).Data, "note.docx").Body);
        await service.DeleteAsync(note.Id); Assert.Empty(await service.ListAsync(null, null));
        await Assert.ThrowsAsync<NotFoundException>(() => service.FileAsync(note.Id));
        Assert.True((await db.InformationNotes.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public void Source_document_parser_preserves_all_paragraphs_and_rejects_fake_or_oversized_files()
    {
        var parsed = InformationNoteDocuments.ReadWord(Bilimfest2026Import.ReadDocument(), "Bilimfest.docx");
        Assert.Equal("15-16-17 Ekim 2026", parsed.DateText); Assert.Contains("57.678 öğrenci", parsed.Body);
        Assert.Contains("Diriliş Cinderesi", parsed.Body); Assert.Contains("bilim kültürünün yaygınlaştırılmasına katkı sağladı.", parsed.Body);
        Assert.Equal(5, parsed.Body.Split('\n').Length);
        Assert.Throws<ArgumentException>(() => InformationNoteDocuments.ValidateFile(new byte[InformationNoteDocuments.MaxBytes + 1], "big.docx"));
        Assert.Throws<ArgumentException>(() => InformationNoteDocuments.ValidateFile("test"u8.ToArray(), "fake.pdf"));
    }
}
