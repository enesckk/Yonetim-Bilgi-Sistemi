using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.SourceSync;

public sealed record SourceNeighborhood(string OfficialCode, string Name, string HeadmanName, string HeadmanPhone, int? Population, int? MaleCount, int? FemaleCount, int? PopulationYear);
public sealed record SourceFacility(Guid SourceId, string Name, string OfficialCode, string Status, string ContactName, string Phone);
public sealed record SourceEvent(Guid SourceId, string Title, string OfficialCode, string Date, string Description, int Attendance);
public sealed record SourceSyncInput(List<SourceNeighborhood> Neighborhoods, List<SourceFacility> Facilities, List<SourceEvent> Events);
public sealed record SourceChange(string Kind, string Key, string Before, string After);
public sealed record SourceSyncPreview(string ReviewToken, bool AlreadyApplied, IReadOnlyList<SourceChange> Changes);
public sealed record SourceSyncApply(SourceSyncInput Data, string ReviewToken);

// The upload is authenticated and never embedded in the public source repository.
// Every change is reviewed against the current target and committed in one transaction.
public sealed class SourceSyncService(AppDbContext db, ICurrentUserService user, IMemoryCache cache)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Serialize(object? value) => JsonSerializer.Serialize(value, Json);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Guid Stable(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0,16));
    private static string Name(string value) => string.Concat(value.Normalize(NormalizationForm.FormC)
        .Where(c => !char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format))
        .ToLower(CultureInfo.GetCultureInfo("tr-TR")).Replace("tiyatroları", "tiyatrosu");
    private static string Marker(SourceSyncInput input) => "SourceSync." + Hash(Serialize(input));
    private void Ensure()
    {
        if (!user.HasPermission(PermissionCodes.EventsManage) || !user.HasPermission(PermissionCodes.OrganizationManage)
            || !user.HasPermission(PermissionCodes.EmployeesViewAllUnits)) throw new ForbiddenException("Kaynak aktarımı için müdürlük kapsamı, organizasyon ve etkinlik yönetimi yetkileri gerekir.");
    }
    private static void Validate(SourceSyncInput data)
    {
        if (data.Neighborhoods is null || data.Facilities is null || data.Events is null
            || data.Neighborhoods.Count > 120 || data.Facilities.Count > 29 || data.Events.Count > 67)
            throw new ValidationException("data", "Aktarım paketinin boyutu geçerli değil.");
        if (data.Neighborhoods.Select(n => n.OfficialCode).Distinct().Count() != data.Neighborhoods.Count
            || data.Facilities.Select(f => f.SourceId).Distinct().Count() != data.Facilities.Count
            || data.Facilities.Select(f => Name(f.Name)).Distinct().Count() != data.Facilities.Count
            || data.Events.Select(e => e.SourceId).Distinct().Count() != data.Events.Count)
            throw new ValidationException("data", "Kaynak paketinde tekrar eden kimlik var.");
        foreach (var n in data.Neighborhoods)
            if (string.IsNullOrWhiteSpace(n.OfficialCode) || n.Name.Length > 200 || n.HeadmanName.Length > 120 || n.HeadmanPhone.Length > 30
                || n.Population < 0 || n.MaleCount < 0 || n.FemaleCount < 0
                || n.Population is not null && (n.PopulationYear is not 2025 || n.MaleCount is null || n.FemaleCount is null || (long)n.MaleCount + n.FemaleCount != n.Population))
                throw new ValidationException("population", "Mahalle, iletişim veya doğrulanmış 2025 nüfus bilgisi geçersiz.");
        foreach (var f in data.Facilities)
            if (f.SourceId == Guid.Empty || string.IsNullOrWhiteSpace(f.Name) || f.Name.Length > 200 || f.ContactName.Length > 120 || f.Phone.Length > 30 || f.Status is not ("aktif" or "bakimda"))
                throw new ValidationException("facility", "Tesis bilgileri geçersiz.");
        foreach (var e in data.Events)
            if (e.SourceId == Guid.Empty || e.Title != "Erzin Kampı" || !e.Date.StartsWith("2024-", StringComparison.Ordinal)
                || !DateOnly.TryParseExact(e.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || e.Description.Length > 4000 || string.IsNullOrWhiteSpace(e.Description) || e.Attendance is < 0 or > 100000)
                throw new ValidationException("event", "Bu aktarım yalnızca doğrulanan Erzin Kampı 2024 eksik kayıtları içindir.");
    }
    public async Task<SourceSyncPreview> PreviewAsync(SourceSyncInput data, CancellationToken ct = default)
    {
        Ensure(); Validate(data);
        db.ChangeTracker.Clear();
        try { return await BuildAsync(data, ct); }
        finally { db.ChangeTracker.Clear(); }
    }
    private async Task<SourceSyncPreview> BuildAsync(SourceSyncInput data, CancellationToken ct)
    {
        var marker = Marker(data);
        if (await db.AppSettings.AnyAsync(s => s.Key == marker, ct)) return new("", true, []);
        var changes = new List<SourceChange>();
        var settlements = await db.Settlements.Where(s => s.IsActive).ToDictionaryAsync(s => s.OfficialCode, ct);
        Settlement Settlement(string code) => settlements.GetValueOrDefault(code) ?? throw new ValidationException("settlement", "Kaynak mahalle kodu hedefte bulunamadı: " + code);
        foreach (var n in data.Neighborhoods)
        {
            var s = Settlement(n.OfficialCode);
            if (Name(s.DisplayName ?? s.Name) != Name(n.Name)) throw new ValidationException("settlement", "Mahalle adı ve kodu eşleşmiyor: " + n.Name);
            var before = Serialize(new { s.HeadmanName, s.HeadmanPhone });
            s.HeadmanName = n.HeadmanName.Trim(); s.HeadmanPhone = n.HeadmanPhone.Trim();
            var after = Serialize(new { s.HeadmanName, s.HeadmanPhone });
            if (before != after) { s.UpdatedBy = user.UserName; changes.Add(new("Muhtar iletişimi", s.OfficialCode, before, after)); }
            if (n.Population is null) continue; // OSB's unqualified zero is not converted into a population record.
            var p = await db.SettlementPopulations.SingleOrDefaultAsync(p => p.SettlementId == s.Id && p.Year == 2025, ct);
            var old = p is null ? "null" : Serialize(new { p.Population, p.MaleCount, p.FemaleCount });
            if (p is null) { p = new() { SettlementId = s.Id, Year = 2025, Source = "YBS_MATCHED_ADNKS2025", SourceReference = "https://ybs.sehitkamil.bel.tr/mahalleler", IsOfficial = false, CreatedBy = user.UserName }; db.SettlementPopulations.Add(p); }
            p.Population = n.Population.Value; p.MaleCount = n.MaleCount; p.FemaleCount = n.FemaleCount;
            var current = Serialize(new { p.Population, p.MaleCount, p.FemaleCount });
            if (old != current) { p.UpdatedBy = user.UserName; changes.Add(new("2025 nüfusu", s.OfficialCode, old, current)); }
        }
        var units = await db.OrganizationUnits.ToListAsync(ct);
        var directorates = units.Where(u => u.Type == OrganizationUnitType.Directorate && Name(u.Name) == Name("Kültür, Sanat ve Sosyal İşler Müdürlüğü")).ToList();
        if (data.Facilities.Count > 0 && directorates.Count != 1) throw new ValidationException("organization", "Hedef müdürlük tekil olarak eşleşmiyor.");
        var ids = units.ToDictionary(u => u.Id);
        bool InDirectorate(OrganizationUnit u)
        {
            var visited = new HashSet<Guid>();
            while (visited.Add(u.Id)) { if (u.Id == directorates[0].Id) return true; if (u.ParentId is not Guid parent || !ids.TryGetValue(parent, out u!)) return false; }
            return false;
        }
        foreach (var f in data.Facilities)
        {
            var matches = units.Where(u => u.Type == OrganizationUnitType.Facility && Name(u.Name) == Name(f.Name)).ToList();
            if (matches.Count > 1 || matches.Count == 1 && !InDirectorate(matches[0])) throw new ValidationException("facility", "Tesis eşleştirmesi belirsiz veya müdürlük dışında: " + f.Name);
            var unit = matches.SingleOrDefault();
            var before = unit is null ? "null" : Serialize(new { unit.Id, unit.Name, unit.Status, unit.ParentId, unit.ContactName, unit.Phone, unit.Address });
            if (unit is null)
            {
                unit = new() { Id = Stable("source-facility:" + f.SourceId), Code = "SRC_" + f.SourceId.ToString("N")[..12], Type = OrganizationUnitType.Facility, ParentId = directorates[0].Id, CreatedBy = user.UserName };
                if (await db.OrganizationUnits.IgnoreQueryFilters().AnyAsync(u => u.Id == unit.Id, ct)) throw new ValidationException("facility", "Tesis daha önce silinmiş; otomatik yeniden oluşturulamaz.");
                db.OrganizationUnits.Add(unit);
            }
            unit.Name = f.Name.Trim(); unit.Status = f.Status == "aktif" ? OrganizationUnitStatus.Active : OrganizationUnitStatus.UnderRenovation;
            unit.ContactName = f.ContactName.Trim(); unit.Phone = f.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(f.OfficialCode)) unit.Address ??= (Settlement(f.OfficialCode).DisplayName ?? Settlement(f.OfficialCode).Name) + " Mahallesi";
            var after = Serialize(new { unit.Id, unit.Name, unit.Status, unit.ParentId, unit.ContactName, unit.Phone, unit.Address });
            if (before != after) { unit.UpdatedBy = user.UserName; changes.Add(new("Tesis / organizasyon şeması", unit.Code ?? unit.Id.ToString(), before, after)); }
        }
        foreach (var e in data.Events)
        {
            var settlement = Settlement(e.OfficialCode);
            var day = DateTime.SpecifyKind(DateTime.ParseExact(e.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeKind.Utc).AddHours(-3);
            var candidates = await db.Events.IgnoreQueryFilters().Include(x => x.Settlements)
                .Where(x => x.Title == e.Title && x.StartAtUtc >= day && x.StartAtUtc < day.AddDays(1)
                    && x.Settlements.Any(s => s.SettlementId == settlement.Id)).ToListAsync(ct);
            var matches = candidates.Where(x => x.Description == e.Description).ToList();
            if (matches.Count > 1 || matches.Any(x => x.IsDeleted || x.Category == EventCategories.Project)) throw new ValidationException("event", "Etkinlik eşleştirmesi belirsiz veya korunmuş kayıt.");
            if (matches.Count == 1)
            {
                if (matches[0].ActualAttendees != e.Attendance) throw new ValidationException("event", "Mevcut Erzin kaydının katılımı farklı; kontrol gerekli.");
                continue;
            }
            if (candidates.Count > 0) throw new ValidationException("event", "Aynı gün ve mahallede açıklaması farklı Erzin kaydı var; mükerrerlik kontrolü gerekli.");
            var id = Stable("source-event:" + e.SourceId);
            if (await db.Events.IgnoreQueryFilters().AnyAsync(x => x.Id == id, ct)) throw new ValidationException("event", "Kaynak kimliği daha önce kullanılmış.");
            var ev = new Event { Id = id, Title = e.Title, Description = e.Description, StartAtUtc = day, IsDateOnly = true, Status = EventStatus.Completed, Category = EventCategories.Trip, ActualAttendees = e.Attendance, SeriesId = Stable("source-program:erzin-2024"), CreatedBy = user.UserName };
            db.Events.Add(ev);
            db.EventSettlements.Add(new EventSettlement { EventId = id, SettlementId = settlement.Id, AttendanceCount = e.Attendance, Notes = e.Description, CreatedBy = user.UserName });
            changes.Add(new("Eksik etkinlik", id.ToString(), "null", Serialize(e)));
        }
        var token = Hash(Serialize(new { Package = Hash(Serialize(data)), Changes = changes }));
        return new(token, false, changes);
    }
    public async Task<SourceSyncPreview> ApplyAsync(SourceSyncApply request, CancellationToken ct = default)
    {
        Ensure(); Validate(request.Data);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261031, 574120)", ct);
            var plan = await BuildAsync(request.Data, ct);
            if (plan.AlreadyApplied) { await tx.CommitAsync(ct); return plan; }
            if (!string.Equals(plan.ReviewToken, request.ReviewToken, StringComparison.Ordinal)) throw new ValidationException("reviewToken", "Hedef kayıtlar veya paket değişti; güncel önizleme alın.");
            // Capture the reviewed before/after values inside the same transaction.
            foreach (var change in plan.Changes) db.AuditLogs.Add(new AuditLog { Action = "SourceSync", EntityName = change.Kind, EntityId = change.Key, UserName = user.UserName, OldValuesJson = change.Before, NewValuesJson = change.After });
            db.AppSettings.Add(new AppSetting { Key = Marker(request.Data), Value = Serialize(new { Changes = plan.Changes.Count, Source = "ybs.sehitkamil.bel.tr", AppliedAt = DateTime.UtcNow }), GroupName = "Kaynak aktarımı", DisplayName = "31 Ekim 2026 kaynak aktarımı", IsReadOnly = true, CreatedBy = user.UserName });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            cache.InvalidateOrgLookups(); cache.InvalidateMapSummaries(); cache.Remove(AppCache.SettlementsAll);
            return plan;
        });
    }
}
