using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>
/// One-time, explicitly requested custody examples (22 October 2026).
/// Separate example people, location and stock; never overwrites operational records.
/// The completion marker also prevents recreating examples after users return/delete them.
/// </summary>
public static class RequestedCustodySamples
{
    public const string MarkerKey = "Stock.RequestedCustodySamples20261022";
    private const string Actor = "requested-custody-examples-20261022";
    private static Guid Id(int number) => Guid.Parse($"7f8a2260-1022-4600-9000-{number:000000000000}");

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await SeedCoreAsync(db, logger, ct);
        }
        catch (ConflictException)
        {
            // Another replica or an operator may have created one of the reserved codes.
            // The transaction rolled back, so never block application startup for examples.
            logger.LogWarning("Requested custody examples skipped after a concurrent code conflict; existing data was preserved.");
        }
    }

    private static async Task SeedCoreAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        await StockTransaction.RunAsync(db, async () =>
        {
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == MarkerKey, ct)) return false;
            var parent = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "KSSIM", ct);
            if (parent is null)
            {
                logger.LogWarning("Requested custody examples skipped: directorate KSSIM is unavailable.");
                return false;
            }
            // If an operator already owns one of these codes, preserve their data and skip the whole set.
            var itemCodes = new[] { "ORNEK-ZIM-LAPTOP", "ORNEK-ZIM-TELSIZ", "ORNEK-ZIM-KULAKLIK", "ORNEK-ZIM-PIL" };
            if (await db.StockItems.IgnoreQueryFilters().AnyAsync(x => itemCodes.Contains(x.Code!), ct) ||
                await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.EmployeeNumber == "ORNEK-ZIM-001" || x.EmployeeNumber == "ORNEK-ZIM-002", ct) ||
                await db.OrganizationUnits.IgnoreQueryFilters().AnyAsync(x => x.Code == "ORNEK_ZIMMET_BIRIM" || x.Code == "ORNEK_ZIMMET_DEPO", ct))
            {
                logger.LogWarning("Requested custody examples skipped: an example code already exists; existing records were preserved.");
                return false;
            }

            var unit = new OrganizationUnit
            {
                Id = Id(1), Name = "[ÖRNEK] Zimmet deneme birimi", Code = "ORNEK_ZIMMET_BIRIM",
                Type = OrganizationUnitType.MainUnit, ParentId = parent.Id,
                Description = "Kullanıcının istediği zimmet örnekleri. Gerçek organizasyon kaydı değildir.", CreatedBy = Actor
            };
            var depot = new OrganizationUnit
            {
                Id = Id(2), Name = "[ÖRNEK] Zimmet deneme deposu", Code = "ORNEK_ZIMMET_DEPO",
                Type = OrganizationUnitType.Facility, ParentId = unit.Id, Parent = unit,
                Description = "Örnek malzemeler için ayrı depo. Gerçek stok değildir.", CreatedBy = Actor
            };
            var people = Enumerable.Range(0, 2).Select(i => new Employee
            {
                Id = Id(10 + i), FirstName = "[ÖRNEK]", LastName = i == 0 ? "Personel A" : "Personel B",
                EmployeeNumber = $"ORNEK-ZIM-{i + 1:000}", UnitId = unit.Id, Unit = unit,
                FacilityId = depot.Id, Facility = depot, Status = EmployeeStatus.Active, CreatedBy = Actor
            }).ToArray();
            var names = new[] { "Dizüstü bilgisayar", "El telsizi", "Koruyucu kulaklık", "Pil (sarf)" };
            var quantities = new decimal[] { 2, 3, 4, 10 };
            var items = Enumerable.Range(0, 4).Select(i => new StockItem
            {
                Id = Id(20 + i), Name = "[ÖRNEK] " + names[i], Code = itemCodes[i],
                Kind = i == 3 ? StockItemKind.Consumable : StockItemKind.FixedAsset,
                Category = i == 0 ? StockCategory.ItEquipment : i == 3 ? StockCategory.Consumables : StockCategory.Other,
                Unit = StockUnit.Piece, Description = "Zimmet denemesi için örnek ürün. Gerçek stok değildir.", CreatedBy = Actor
            }).ToArray();
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            db.OrganizationUnits.AddRange(unit, depot);
            db.Employees.AddRange(people);
            db.StockItems.AddRange(items);
            for (var i = 0; i < items.Length; i++)
            {
                db.StockBalances.Add(new StockBalance
                {
                    Id = Id(30 + i), StockItem = items[i], StockItemId = items[i].Id, Location = depot, LocationId = depot.Id,
                    Quantity = quantities[i], Notes = "[ÖRNEK] Başlangıç stoğu", CreatedBy = Actor
                });
                db.StockMovements.Add(new StockMovement
                {
                    Id = Id(40 + i), StockItem = items[i], StockItemId = items[i].Id, ToLocation = depot, ToLocationId = depot.Id,
                    MovementType = StockMovementType.Inbound, Quantity = quantities[i], OccurredOn = today,
                    Reason = "[ÖRNEK] Kullanıcının istediği zimmet deneme verileri", CreatedBy = Actor
                });
            }
            for (var i = 0; i < 3; i++)
            {
                var person = people[i == 1 ? 1 : 0];
                db.StockCustodies.Add(new StockCustody
                {
                    Id = Id(50 + i), StockItem = items[i], StockItemId = items[i].Id, Location = depot, LocationId = depot.Id,
                    Employee = person, EmployeeId = person.Id, Quantity = i == 2 ? 2 : 1, Unit = StockUnit.Piece,
                    SerialNumber = i == 0 ? "ORNEK-LAPTOP-001" : i == 1 ? "ORNEK-TELSIZ-001" : null,
                    AssignedOn = today, Notes = "[ÖRNEK] Personel değiştirme, malzeme değiştirme ve iade denemesi.", CreatedBy = Actor
                });
            }
            db.AppSettings.Add(new AppSetting
            {
                Id = Id(60), Key = MarkerKey, Value = "true", ValueType = AppSettingValueType.Boolean,
                GroupName = "Stok", DisplayName = "İstenen örnek zimmet verileri oluşturuldu", IsReadOnly = true,
                Description = "22.10.2026 tarihinde istenen ayrı örnek personel, depo ve malzemeler bir defa eklendi.", CreatedBy = Actor
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Requested custody examples created: 2 example employees, 4 items and 3 custodies in a separate example depot.");
            return true;
        }, ct);
    }
}
