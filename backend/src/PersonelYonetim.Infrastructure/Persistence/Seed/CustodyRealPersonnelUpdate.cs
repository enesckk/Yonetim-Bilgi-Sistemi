using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>Reconciles the user's sample stock with existing personnel; never creates people.</summary>
public static class CustodyRealPersonnelUpdate
{
    public const string Marker = "Stock.RealPersonnelCustodies20261022";
    private const string LegacyActor = "requested-custody-examples-20261022";
    private const string Actor = "requested-real-personnel-custodies";

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await StockTransaction.RunAsync(db, async () =>
        {
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == Marker, ct)) return false;
            var facilities = await StockAccess.StockableUnits(db).Where(x => x.CreatedBy != LegacyActor)
                .ToDictionaryAsync(x => x.Id, ct);
            var people = await db.Employees.Where(x => x.Status == EmployeeStatus.Active && x.CreatedBy != LegacyActor &&
                    !x.FirstName.StartsWith("[ÖRNEK]") && x.FacilityId != null)
                .OrderBy(x => x.EmployeeNumber).ThenBy(x => x.LastName).ThenBy(x => x.Id).ToListAsync(ct);
            people = people.Where(x => facilities.ContainsKey(x.FacilityId!.Value)).ToList();
            if (people.Count == 0)
            {
                logger.LogWarning("Custody reconciliation postponed: no existing active employee has an available facility.");
                return false;
            }
            var preferred = new[] { ("Ahmet", "Oral"), ("Abdullah", "Orhan"), ("Ali", "Döner") };
            var targets = new List<Employee>();
            foreach (var (first, last) in preferred)
            {
                var target = people.FirstOrDefault(x => x.FirstName == first && x.LastName == last && targets.All(t => t.Id != x.Id))
                    ?? people.FirstOrDefault(x => targets.All(t => t.Id != x.Id)) ?? people[0];
                targets.Add(target);
            }
            var codes = new[] { "ORNEK-ZIM-LAPTOP", "ORNEK-ZIM-TELSIZ", "ORNEK-ZIM-KULAKLIK", "ORNEK-ZIM-PIL" };
            var names = new[] { "Dizüstü bilgisayar", "El telsizi", "Koruyucu kulaklık", "Pil" };
            var totals = new decimal[] { 2, 3, 4, 10 };
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var updated = 0;
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i];
                var person = targets[i == 3 ? 0 : i];
                var locationId = person.FacilityId!.Value;
                var item = await db.StockItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Code == code, ct);
                var fresh = item is null;
                if (item is not null && (item.IsDeleted || !item.IsActive || item.CreatedBy != LegacyActor)) continue;
                if (fresh)
                {
                    item = new StockItem
                    {
                        Code = code, Name = names[i], Kind = i == 3 ? StockItemKind.Consumable : StockItemKind.FixedAsset,
                        Category = i == 0 ? StockCategory.ItEquipment : i == 3 ? StockCategory.Consumables : StockCategory.Other,
                        Unit = StockUnit.Piece, CreatedBy = Actor,
                        Description = "Kullanıcı isteğiyle eklenen örnek stok; kayıtlı personele zimmet denemesi için."
                    };
                    db.StockItems.Add(item);
                    await db.SaveChangesAsync(ct);
                }
                else if (item!.UpdatedBy is null)
                {
                    item.Name = names[i];
                    item.Description = "Kullanıcı isteğiyle eklenen örnek stok; kayıtlı personele zimmet denemesi için.";
                    item.UpdatedBy = Actor;
                    item.UpdatedAtUtc = DateTime.UtcNow;
                }

                var old = await db.StockCustodies.Include(x => x.Employee)
                    .FirstOrDefaultAsync(x => x.StockItemId == item!.Id && x.ReturnedOn == null &&
                        x.CreatedBy == LegacyActor && x.UpdatedBy == null && x.Employee.CreatedBy == LegacyActor, ct);
                // Returned, transferred or manually edited records belong to the operator and stay untouched.
                if (!fresh && i < 3 && old is null) continue;

                var source = fresh ? null : await db.StockBalances.Include(x => x.Location).FirstOrDefaultAsync(x =>
                    x.StockItemId == item!.Id && x.Location != null && x.Location.CreatedBy == LegacyActor, ct);
                if (!fresh)
                {
                    if (source is null || source.Quantity <= 0 || source.UpdatedBy is not null) continue;
                    var others = await db.StockCustodies.AnyAsync(x => x.StockItemId == item!.Id &&
                        x.LocationId == source.LocationId && x.ReturnedOn == null && (old == null || x.Id != old.Id), ct);
                    if (others || (old is not null && source.Quantity < old.Quantity)) continue;
                }
                var quantity = fresh ? totals[i] : source!.Quantity;
                var balance = await db.StockBalances.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
                    x.StockItemId == item!.Id && x.LocationId == locationId, ct);
                if (balance is null)
                {
                    balance = new StockBalance { StockItemId = item!.Id, LocationId = locationId, CreatedBy = Actor };
                    db.StockBalances.Add(balance);
                }
                if (balance.IsDeleted)
                {
                    balance.IsDeleted = false;
                    balance.DeletedAtUtc = null;
                    balance.DeletedBy = null;
                    balance.Quantity = 0;
                }
                balance.Quantity += quantity;
                if (source is not null)
                {
                    source.Quantity = 0;
                    source.UpdatedBy = Actor;
                    source.UpdatedAtUtc = DateTime.UtcNow;
                }
                db.StockMovements.Add(new StockMovement
                {
                    StockItemId = item!.Id, MovementType = fresh ? StockMovementType.Inbound : StockMovementType.Transfer,
                    FromLocationId = source?.LocationId, ToLocationId = locationId, Quantity = quantity, OccurredOn = today,
                    Reason = "Örnek stokların kayıtlı personelin tesisine alınması.", CreatedBy = Actor
                });
                if (old is not null)
                {
                    old.ReturnedOn = today;
                    old.ReturnNotes = "Kullanıcı isteğiyle kayıtlı personele ve tesisine aktarıldı.";
                    old.UpdatedBy = Actor;
                    old.UpdatedAtUtc = DateTime.UtcNow;
                    // Release the unique active serial inside this transaction before reusing it.
                    await db.SaveChangesAsync(ct);
                }
                if (i < 3)
                {
                    db.StockCustodies.Add(new StockCustody
                    {
                        StockItemId = item.Id, LocationId = locationId, EmployeeId = person.Id,
                        Quantity = old?.Quantity ?? (i == 2 ? 2 : 1), Unit = old?.Unit ?? item.Unit,
                        SerialNumber = old?.SerialNumber ?? (i == 0 ? "ORNEK-LAPTOP-001" : i == 1 ? "ORNEK-TELSIZ-001" : null),
                        AssignedOn = today, ReplacesId = old?.Id, Notes = null, CreatedBy = Actor
                    });
                    updated++;
                }
                await db.SaveChangesAsync(ct);
            }

            var fakePeople = await db.Employees.Where(x => x.CreatedBy == LegacyActor).ToListAsync(ct);
            foreach (var fake in fakePeople)
                if (!await db.StockCustodies.AnyAsync(x => x.EmployeeId == fake.Id && x.ReturnedOn == null, ct) &&
                    !await db.Users.AnyAsync(x => x.EmployeeId == fake.Id, ct))
                {
                    fake.IsDeleted = true;
                    fake.Status = EmployeeStatus.Passive;
                    fake.DeletedBy = Actor;
                    fake.DeletedAtUtc = DateTime.UtcNow;
                }
            var oldFacilities = await db.OrganizationUnits.Where(x => x.CreatedBy == LegacyActor && x.Type == OrganizationUnitType.Facility).ToListAsync(ct);
            foreach (var oldFacility in oldFacilities)
                if (!await db.StockBalances.AnyAsync(x => x.LocationId == oldFacility.Id && x.Quantity > 0, ct) &&
                    !await db.StockCustodies.AnyAsync(x => x.LocationId == oldFacility.Id && x.ReturnedOn == null, ct) &&
                    !await db.Employees.AnyAsync(x => x.FacilityId == oldFacility.Id && x.CreatedBy != LegacyActor, ct))
                {
                    oldFacility.IsDeleted = true;
                    oldFacility.DeletedBy = Actor;
                    oldFacility.DeletedAtUtc = DateTime.UtcNow;
                }
            await db.SaveChangesAsync(ct);
            var oldUnits = await db.OrganizationUnits.Where(x => x.CreatedBy == LegacyActor && x.Type != OrganizationUnitType.Facility).ToListAsync(ct);
            foreach (var oldUnit in oldUnits)
                if (!await db.OrganizationUnits.AnyAsync(x => x.ParentId == oldUnit.Id, ct) &&
                    !await db.Employees.AnyAsync(x => x.UnitId == oldUnit.Id, ct))
                {
                    oldUnit.IsDeleted = true;
                    oldUnit.DeletedBy = Actor;
                    oldUnit.DeletedAtUtc = DateTime.UtcNow;
                }
            db.AppSettings.Add(new AppSetting
            {
                Key = Marker, Value = "true", ValueType = AppSettingValueType.Boolean, GroupName = "Stok", IsReadOnly = true,
                DisplayName = "Zimmetler kayıtlı personele aktarıldı", CreatedBy = Actor,
                Description = "Kullanıcının istediği örnek stoklar, mevcut aktif personel ve tesislere bir defa aktarıldı."
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Custody reconciliation completed: {Count} initial examples assigned to existing personnel.", updated);
            return true;
        }, ct);
    }
}
