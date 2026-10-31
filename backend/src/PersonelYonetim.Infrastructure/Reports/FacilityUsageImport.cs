using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Reports;

public sealed record SourceFacilityUsage(string FacilityName, int Year, int Month, long ParticipantCount);
// Invoked by the authorized maintenance process, never by an anonymous web endpoint.
public static class FacilityUsageImport
{
    public const string Source = "https://ybs.sehitkamil.bel.tr/api/api/Reports/facilities";
    public const string Marker = "Source.FacilityUsage.20261031";
    public const string Actor = "source-facility-usage-20261031";
    private static string Name(string value) => string.Concat(value.Normalize().Where(c => !char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format)).ToLower(CultureInfo.GetCultureInfo("tr-TR")).Replace("tiyatroları", "tiyatrosu");
    public static async Task<int> ImportAsync(AppDbContext db, IReadOnlyList<SourceFacilityUsage> rows, CancellationToken ct = default)
    {
        if (rows.Count != 333 || rows.Sum(r => r.ParticipantCount) != 3821850 || rows.Any(r => r.Year is < 2024 or > 2026 || r.Month is < 1 or > 12 || r.ParticipantCount < 0 || string.IsNullOrWhiteSpace(r.FacilityName))) throw new InvalidOperationException("Kaynak tesis raporunun 333 kayıt ve 3.821.850 katılım kontrolü geçmedi.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261031, 333)", ct);
            var units = await db.OrganizationUnits.Where(u => u.Type == OrganizationUnitType.Facility).ToListAsync(ct); var occurrences = new Dictionary<string, int>(); var prepared = new List<FacilityUsageRecord>();
            foreach (var row in rows)
            {
                var matches = units.Where(u => Name(u.Name) == Name(row.FacilityName)).ToList(); if (matches.Count != 1) throw new InvalidOperationException("Tesis raporu eşleştirmesi tekil değil: " + row.FacilityName);
                var basis = $"{matches[0].Id}|{row.Year}|{row.Month}|{row.ParticipantCount}"; var occurrence = occurrences.GetValueOrDefault(basis); occurrences[basis] = occurrence + 1;
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis + "|" + occurrence));
                prepared.Add(new() { Id = new Guid(hash.AsSpan(0, 16)), FacilityId = matches[0].Id, Year = row.Year, Month = row.Month, ParticipantCount = row.ParticipantCount, SourceKey = Convert.ToHexString(hash), SourceReference = Source, CreatedBy = Actor });
            }
            var existing = await db.FacilityUsageRecords.IgnoreQueryFilters().ToListAsync(ct);
            if (await db.AppSettings.AnyAsync(s => s.Key == Marker, ct))
            {
                if (prepared.Any(p => !existing.Any(e => !e.IsDeleted && e.SourceKey == p.SourceKey && e.FacilityId == p.FacilityId && e.Year == p.Year && e.Month == p.Month && e.ParticipantCount == p.ParticipantCount))) throw new InvalidOperationException("Önceden aktarılan aylık kayıtlar farklı; üzerine yazılmadı.");
                await tx.CommitAsync(ct); return 0;
            }
            if (existing.Any()) throw new InvalidOperationException("Aylık tesis kayıtları zaten var; mükerrerlik incelemesi gerekli.");
            db.FacilityUsageRecords.AddRange(prepared); await db.SaveChangesAsync(ct);
            if (await db.FacilityUsageRecords.CountAsync(ct) != 333 || await db.FacilityUsageRecords.SumAsync(r => r.ParticipantCount, ct) != 3821850) throw new InvalidOperationException("Tesis raporu kayıt sonrası toplamları uyuşmuyor.");
            var receipt = JsonSerializer.Serialize(new { Rows = 333, Attendance = 3821850, Facilities = prepared.Select(p => p.FacilityId).Distinct().Count(), Source });
            db.AppSettings.Add(new AppSetting { Key = Marker, Value = receipt, IsReadOnly = true, DisplayName = "Kaynak aylık tesis raporları", GroupName = "Raporlar", CreatedBy = Actor });
            db.AuditLogs.Add(new AuditLog { Action = "Import", EntityName = "FacilityUsageRecord", EntityId = Marker, UserName = Actor, OldValuesJson = "[]", NewValuesJson = receipt });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return prepared.Count;
        });
    }
}
