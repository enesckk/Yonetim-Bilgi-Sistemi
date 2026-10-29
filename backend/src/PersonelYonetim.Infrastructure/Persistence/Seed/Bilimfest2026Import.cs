using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.InformationNotes;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class Bilimfest2026Import
{
    public static readonly Guid ProjectId = Guid.Parse("94c84da0-df74-450a-9fe4-5798aa57f202");
    public static readonly Guid NoteId = Guid.Parse("f4bc51e5-16c6-4f01-bbb2-4d908e9bf202");
    public const string Marker = "Events.UserBilimfest2026.20261030";
    public static byte[] ReadDocument()
    {
        using var stream = typeof(Bilimfest2026Import).Assembly.GetManifestResourceStream("PersonelYonetim.Infrastructure.Persistence.Seed.bilimfest-2026-user-note.docx") ?? throw new InvalidOperationException("BilimFest bilgi notu bulunamadı.");
        using var ms = new MemoryStream(); stream.CopyTo(ms); var bytes = ms.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != "5219b7780734fda9194f80f3a74f502b527c1676a0636a3bb70b35c8458e7285") throw new InvalidOperationException("BilimFest bilgi notu kaynak doğrulaması başarısız.");
        return bytes;
    }
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261030, 57678)", ct);
            else if (db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'Bilimfest2026Import', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000", ct);
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(s => s.Key == Marker, ct)) { await tx.CommitAsync(ct); return; }
            var file = ReadDocument(); var source = InformationNoteDocuments.ReadWord(file, "Bilimfest Gaziantep Bilgi Notu.docx");
            var facility = await db.OrganizationUnits.SingleOrDefaultAsync(u => u.Code == "KKM" && u.Type == OrganizationUnitType.Facility, ct) ?? throw new InvalidOperationException("BilimFest için Kültür ve Kongre Merkezi eşleşmedi; kayıt eklenmedi.");
            var date = new DateTime(2026, 10, 14, 21, 0, 0, DateTimeKind.Utc);
            var candidates = await db.Events.IgnoreQueryFilters().Where(e => e.Id == ProjectId || ((e.Title.Contains("BilimFest") || e.Title.Contains("Bilimfest")) && e.StartAtUtc >= date && e.StartAtUtc < date.AddDays(3))).ToListAsync(ct);
            if (candidates.Count > 1 || candidates.Any(e => e.IsDeleted || e.Category != EventCategories.Project || e.ActualAttendees != 57678 || e.FacilityId != facility.Id)) throw new InvalidOperationException("Mevcut BilimFest kaydı kaynak belgeyle uyuşmuyor; içe aktarım geri alındı.");
            var project = candidates.SingleOrDefault() ?? new Event { Id = ProjectId, Title = "TÜBİTAK 4007 – BilimFest Gaziantep 2026", Description = source.Body, Category = EventCategories.Project,
                StartAtUtc = date, EndAtUtc = date.AddDays(3).AddTicks(-1), IsDateOnly = true, Status = EventStatus.Completed, ActualAttendees = 57678,
                FacilityId = facility.Id, Address = "Şehitkamil Kültür ve Kongre Merkezi", CreatedBy = "user-bilimfest-20261030" };
            if (candidates.Count == 0) db.Events.Add(project);
            if (await db.InformationNotes.IgnoreQueryFilters().AnyAsync(n => n.Id == NoteId, ct)) throw new InvalidOperationException("BilimFest bilgi notu kimliği önceden kullanılmış; içe aktarım geri alındı.");
            db.InformationNotes.Add(new InformationNote { Id = NoteId, EventId = project.Id, Title = "BilimFest Gaziantep 2026 Bilgi Notu", DateText = source.DateText, Organizer = source.Organizer, Subject = source.Subject,
                PreparedBy = source.PreparedBy, Body = source.Body, FileData = file, FileName = "Bilimfest Gaziantep Bilgi Notu.docx", ContentType = InformationNoteDocuments.DocxType, CreatedBy = "user-bilimfest-20261030" });
            db.AuditLogs.Add(new AuditLog { Action = "Import", EntityName = "InformationNote", EntityId = NoteId.ToString(), UserName = "user-bilimfest-20261030" });
            await db.SaveChangesAsync(ct);
            var saved = await db.InformationNotes.AsNoTracking().Include(n => n.Event).SingleAsync(n => n.Id == NoteId, ct);
            if (saved.Body != source.Body || !saved.FileData!.SequenceEqual(file) || saved.Event!.ActualAttendees != 57678 || saved.Event.Category != EventCategories.Project || saved.Event.Status != EventStatus.Completed)
                throw new InvalidOperationException("BilimFest kayıt sonrası doğrulaması başarısız; işlem geri alındı.");
            db.AppSettings.Add(new AppSetting { Key = Marker, Value = "1 proje; 57678 katılım; 87 atölye; 1 özgün bilgi notu", GroupName = "Etkinlikler", DisplayName = "Kullanıcı BilimFest 2026 projesi ve bilgi notu işlendi", IsReadOnly = true });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            logger.LogInformation("BilimFest 2026 projesi ve özgün bilgi notu doğrulandı: 15–17 Ekim, 57678 katılım, 87 atölye.");
        });
    }
}
