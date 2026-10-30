using System.Data;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Infrastructure.Reports;
namespace PersonelYonetim.Infrastructure.Persistence.Seed;
public static class ActivityReport2026Seed
{
    public static readonly Guid ReportId = Guid.Parse("c6e39703-ae9c-44f8-950b-202600000001");
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync((db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261030, 30001)", ct);
            else if (db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'ActivityReport2026', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000", ct);
            if (!await db.ActivityReports.IgnoreQueryFilters().AnyAsync(r => r.Id == ReportId, ct)
                && await db.Events.AnyAsync(e => e.StartAtUtc >= new DateTime(2025, 12, 31, 21, 0, 0, DateTimeKind.Utc) && e.StartAtUtc < new DateTime(2026, 12, 31, 21, 0, 0, DateTimeKind.Utc), ct))
                await ActivityReportService.GenerateCoreAsync(db, new("2026 Faaliyet Raporu", new(2026, 1, 1), new(2026, 12, 31)), "system-2026-report", ReportId, ct);
            await tx.CommitAsync(ct);
        });
    }
}
