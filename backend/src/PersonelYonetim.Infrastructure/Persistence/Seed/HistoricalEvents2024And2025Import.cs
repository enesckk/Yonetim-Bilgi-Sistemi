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

/// <summary>Atomic, one-time import of the 473 historical rows explicitly provided by the user.</summary>
public static class HistoricalEvents2024And2025Import
{
    public const string Marker = "Events.UserHistorical2024And2025.20261029";
    public const string Actor = "user-historical-events-20261029";
    public sealed record Row(string Program, string Title, string Settlement, string OfficialCode, string Date, string Description, int Count);
    public static Guid ProgramId(Row row) => StableId($"user:historical-events:{row.Program}");
    public static Guid EventId(Row row) => StableId($"historical-events|{row.Program}|{row.Date}|{row.OfficialCode}|{row.Description}");
    public static readonly IReadOnlyDictionary<string, (int Records, int Attendance)> Expected = new Dictionary<string, (int, int)>
    {
        ["ukkaşe-2025"] = (60, 7200), ["theatre-2025"] = (37, 11200),
        ["ankara-2025"] = (73, 999), ["samsun-2025"] = (72, 922),
        ["erzin-2025"] = (69, 945), ["samsun-2024"] = (75, 773),
        ["urfa-2025"] = (16, 1610), ["theatre-2024"] = (15, 3200), ["ankara-2024"] = (56, 227)
    };
    public static bool IsTheatre(Row row) => row.Program.StartsWith("theatre-", StringComparison.Ordinal);

    public static IReadOnlyList<Row> ReadRows()
    {
        using var stream = typeof(HistoricalEvents2024And2025Import).Assembly.GetManifestResourceStream(
            "PersonelYonetim.Infrastructure.Persistence.Seed.historical-events-2024-2025-user-import.json")
            ?? throw new InvalidOperationException("Tarihsel etkinlik kaynak listesi bulunamadı.");
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (rows.Count != 473 || rows.Sum(r => r.Count) != 27076
            || rows.Any(r => r.Description.Length > 4000 || r.Count <= 0 || !Expected.ContainsKey(r.Program)
                || !r.Date.StartsWith(r.Program[^4..], StringComparison.Ordinal))
            || rows.Select(EventId).Distinct().Count() != rows.Count
            || Expected.Any(p => rows.Count(r => r.Program == p.Key) != p.Value.Records
                || rows.Where(r => r.Program == p.Key).Sum(r => r.Count) != p.Value.Attendance))
            throw new InvalidOperationException("Tarihsel etkinlik kaynak toplamları uyuşmuyor.");
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
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261029, 204473)", ct);
            else if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'HistoricalEvents2024And2025Import', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000", ct);
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == Marker, ct)) { await tx.CommitAsync(ct); return; }

            var rows = ReadRows();
            var settlements = await db.Settlements.Where(x => x.IsActive).ToDictionaryAsync(x => x.OfficialCode, ct);
            var facilities = await db.OrganizationUnits.Where(x => x.Code == "DTSS" && x.Type == OrganizationUnitType.Facility).ToListAsync(ct);
            if (facilities.Count != 1 || rows.Any(r => !settlements.ContainsKey(r.OfficialCode)))
                throw new InvalidOperationException("Tarihsel etkinlikler için mevcut mahalle/Devlet Tiyatroları tesisi tam eşleşmedi; hiçbir kayıt eklenmedi.");
            var facility = facilities[0];
            var start = StartUtc(rows.MinBy(r => r.Date)!);
            var end = StartUtc(rows.MaxBy(r => r.Date)!).AddDays(1);
            var existing = await db.Events.IgnoreQueryFilters().Include(x => x.Settlements)
                .Where(e => e.StartAtUtc >= start && e.StartAtUtc < end).ToListAsync(ct);
            var imported = new List<Guid>();
            var added = 0;
            foreach (var row in rows)
            {
                var settlement = settlements[row.OfficialCode];
                var date = StartUtc(row);
                Guid? facilityId = IsTheatre(row) ? facility.Id : null;
                var matches = existing.Where(e => e.Title == row.Title && e.Description == row.Description && e.StartAtUtc >= date && e.StartAtUtc < date.AddDays(1)
                    && e.Settlements.Any(s => s.SettlementId == settlement.Id)).ToList();
                if (matches.Count > 1 || matches.Any(e => e.IsDeleted || e.Status != EventStatus.Completed || e.ActualAttendees != row.Count || e.FacilityId != facilityId))
                    throw new InvalidOperationException("Tarihsel etkinlik mevcut kayıt eşleştirmesi belirsiz; içe aktarım geri alındı.");
                if (matches.Count == 1)
                {
                    matches[0].SeriesId ??= ProgramId(row);
                    matches[0].IsDateOnly = true;
                    foreach (var link in matches[0].Settlements.Where(s => s.SettlementId == settlement.Id && !s.IsDeleted))
                        link.Notes ??= row.Description;
                    imported.Add(matches[0].Id);
                    continue;
                }
                var ev = new Event
                {
                    Id = EventId(row),
                    Title = row.Title, Description = row.Description,
                    StartAtUtc = date, IsDateOnly = true, EndAtUtc = null,
                    Status = EventStatus.Completed, Category = IsTheatre(row) ? EventCategories.Culture : EventCategories.Trip,
                    ActualAttendees = row.Count, ExpectedAttendees = null,
                    FacilityId = facilityId, Address = IsTheatre(row) ? "Devlet Tiyatroları" : row.Title, SeriesId = ProgramId(row),
                    RecurrenceFrequency = EventRecurrenceFrequency.None, CreatedBy = Actor
                };
                db.Events.Add(ev);
                db.EventSettlements.Add(new EventSettlement
                {
                    EventId = ev.Id, SettlementId = settlement.Id, AttendanceCount = row.Count,
                    UniqueBeneficiaryCount = null, Notes = row.Description, CreatedBy = Actor
                });
                db.AuditLogs.Add(new AuditLog { Action = "Create", EntityName = "Event", EntityId = ev.Id.ToString(), UserName = Actor,
                    NewValuesJson = JsonSerializer.Serialize(new { source = "Kullanıcı tarihsel etkinlik metni", row.Program, row.Date, row.Settlement, row.Count }) });
                imported.Add(ev.Id);
                added++;
            }
            await db.SaveChangesAsync(ct);
            // Verify the persisted records and all neighborhood links before committing the receipt.
            var saved = await db.Events.AsNoTracking().Include(x => x.Settlements).Where(e => imported.Contains(e.Id)).ToListAsync(ct);
            if (saved.Count != 473 || saved.Sum(e => e.ActualAttendees ?? 0) != 27076)
                throw new InvalidOperationException("Tarihsel etkinlik kayıt sonrası toplamı uyuşmuyor; işlem geri alındı.");
            foreach (var (row, id) in rows.Zip(imported))
            {
                var ev = saved.Single(e => e.Id == id);
                var links = ev.Settlements.Where(s => !s.IsDeleted).ToList();
                if (ev.SeriesId != ProgramId(row) || ev.Description != row.Description || ev.Title != row.Title
                    || !ev.IsDateOnly || ev.StartAtUtc != StartUtc(row) || ev.ActualAttendees != row.Count
                    || links.Count != 1 || links[0].SettlementId != settlements[row.OfficialCode].Id
                    || links[0].AttendanceCount != row.Count || links[0].Notes != row.Description)
                    throw new InvalidOperationException("Tarihsel etkinlik kayıt/mahalle doğrulaması başarısız; işlem geri alındı.");
            }
            db.AppSettings.Add(new AppSetting { Key = Marker, Value = "473 kayıt; 27076 katılım; 9 program", GroupName = "Etkinlikler", DisplayName = "Kullanıcı 2024 ve 2025 etkinlik listeleri işlendi", IsReadOnly = true, CreatedBy = Actor });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("Kullanıcı tarihsel etkinlik içe aktarımı doğrulandı: {Added} eklendi, {Reused} mevcut; 473 kayıt, 27076 katılım, 9 program.", added, 473 - added);
        });
    }
}
