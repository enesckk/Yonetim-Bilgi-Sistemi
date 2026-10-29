using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>One-time import of the exact 34 records explicitly provided by the user.</summary>
public static class ChildTheatre2026Import
{
    public const string Marker = "Events.UserChildTheatre2026.20261029";
    public const string Actor = "user-child-theatre-2026-20261029";
    public sealed record Row(string Settlement, string OfficialCode, string Date, string Description, int Count, string School);
    public static Guid ProgramId => StableId("user:child-theatre:2026");

    public static IReadOnlyList<Row> ReadRows()
    {
        using var stream = typeof(ChildTheatre2026Import).Assembly.GetManifestResourceStream(
            "PersonelYonetim.Infrastructure.Persistence.Seed.child-theatre-2026-user-import.json")
            ?? throw new InvalidOperationException("Çocuk tiyatrosu kaynak listesi bulunamadı.");
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (rows.Count != 34 || rows.Sum(r => r.Count) != 8000 || rows.Any(r => r.Description.Length > 4000 || r.Count <= 0 || !r.Description.StartsWith(r.School, StringComparison.Ordinal)))
            throw new InvalidOperationException("Çocuk tiyatrosu kaynak toplamları uyuşmuyor.");
        return rows;
    }

    // The source provides Istanbul calendar dates, not event times.
    public static DateTime StartUtc(Row row) => new DateTimeOffset(DateTime.ParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture), TimeSpan.FromHours(3)).UtcDateTime;

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261029, 203034)", ct);
            else if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'ChildTheatre2026Import', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000", ct);
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == Marker, ct)) { await tx.CommitAsync(ct); return; }

            var rows = ReadRows();
            var settlements = await db.Settlements.Where(x => x.IsActive).ToDictionaryAsync(x => x.OfficialCode, ct);
            var facilities = await db.OrganizationUnits.Where(x => x.Code == "DTSS" && x.Type == OrganizationUnitType.Facility).ToListAsync(ct);
            if (facilities.Count != 1 || rows.Any(r => !settlements.ContainsKey(r.OfficialCode)))
                throw new InvalidOperationException("Çocuk tiyatrosu için mevcut mahalle/Devlet Tiyatroları tesisi tam eşleşmedi; hiçbir kayıt eklenmedi.");
            var facility = facilities[0];
            var start = StartUtc(rows.MinBy(r => r.Date)!);
            var end = StartUtc(rows.MaxBy(r => r.Date)!).AddDays(1);
            var existing = await db.Events.IgnoreQueryFilters().Include(x => x.Settlements)
                .Where(e => e.Title == "Çocuk Tiyatrosu" && e.StartAtUtc >= start && e.StartAtUtc < end).ToListAsync(ct);
            var imported = new List<Guid>();
            var added = 0;
            foreach (var row in rows)
            {
                var settlement = settlements[row.OfficialCode];
                var date = StartUtc(row);
                var matches = existing.Where(e => e.Description == row.Description && e.StartAtUtc >= date && e.StartAtUtc < date.AddDays(1)
                    && e.Settlements.Any(s => s.SettlementId == settlement.Id)).ToList();
                if (matches.Count > 1 || matches.Any(e => e.IsDeleted || e.Status != EventStatus.Completed || e.ActualAttendees != row.Count || e.FacilityId != facility.Id))
                    throw new InvalidOperationException("Çocuk tiyatrosu mevcut kayıt eşleştirmesi belirsiz; içe aktarım geri alındı.");
                if (matches.Count == 1)
                {
                    matches[0].SeriesId ??= ProgramId;
                    foreach (var link in matches[0].Settlements.Where(s => s.SettlementId == settlement.Id && !s.IsDeleted))
                        link.Notes ??= row.Description;
                    imported.Add(matches[0].Id);
                    continue;
                }
                var ev = new Event
                {
                    Id = StableId($"child-theatre-2026|{row.Date}|{row.OfficialCode}|{row.School}"),
                    Title = "Çocuk Tiyatrosu", Description = row.Description,
                    StartAtUtc = date, IsDateOnly = true, EndAtUtc = null,
                    Status = EventStatus.Completed, Category = EventCategories.Culture,
                    ActualAttendees = row.Count, ExpectedAttendees = null,
                    FacilityId = facility.Id, Address = "Devlet Tiyatroları", SeriesId = ProgramId,
                    RecurrenceFrequency = EventRecurrenceFrequency.None, CreatedBy = Actor
                };
                db.Events.Add(ev);
                db.EventSettlements.Add(new EventSettlement
                {
                    EventId = ev.Id, SettlementId = settlement.Id, AttendanceCount = row.Count,
                    UniqueBeneficiaryCount = null, Notes = row.Description, CreatedBy = Actor
                });
                db.AuditLogs.Add(new AuditLog { Action = "Create", EntityName = "Event", EntityId = ev.Id.ToString(), UserName = Actor,
                    NewValuesJson = JsonSerializer.Serialize(new { source = "Kullanıcı metni: Çocuk Tiyatrosu 2026", row.Date, row.Settlement, row.Count }) });
                imported.Add(ev.Id);
                added++;
            }
            await db.SaveChangesAsync(ct);
            // Verify the persisted records and all neighborhood links before committing the receipt.
            var saved = await db.Events.AsNoTracking().Include(x => x.Settlements).Where(e => imported.Contains(e.Id)).ToListAsync(ct);
            if (saved.Count != 34 || saved.Any(e => e.SeriesId != ProgramId) || saved.Sum(e => e.ActualAttendees ?? 0) != 8000 || saved.Sum(e => e.Settlements.Where(s => !s.IsDeleted).Sum(s => s.AttendanceCount)) != 8000)
                throw new InvalidOperationException("Çocuk tiyatrosu kayıt sonrası kontrolü başarısız; içe aktarım geri alındı.");
            db.AppSettings.Add(new AppSetting { Key = Marker, Value = "34 kayıt; 8000 katılım; 29 mahalle", GroupName = "Etkinlikler", DisplayName = "Kullanıcı Çocuk Tiyatrosu 2026 listesi işlendi", IsReadOnly = true, CreatedBy = Actor });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("Kullanıcı çocuk tiyatrosu içe aktarımı doğrulandı: {Added} eklendi, {Reused} mevcut, 34 kayıt, 8000 katılım.", added, 34 - added);
        });
    }
}
