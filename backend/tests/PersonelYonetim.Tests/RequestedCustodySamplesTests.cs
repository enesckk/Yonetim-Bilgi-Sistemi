using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Tests;

public sealed class RequestedCustodySamplesTests
{
    [Fact]
    public async Task Creates_isolated_examples_once_without_changing_real_stock_or_assigning_real_personnel()
    {
        await using var f = await Fixture.Create();
        await f.Seed();
        await f.Seed();
        Assert.Equal(3, await f.Db.Employees.CountAsync());
        Assert.Equal(5, await f.Db.StockItems.CountAsync());
        Assert.Equal(3, await f.Db.StockCustodies.CountAsync());
        Assert.Equal(4, await f.Db.StockMovements.CountAsync());
        Assert.Equal(7, await f.Db.StockBalances.Where(x => x.StockItemId == f.RealItem.Id).Select(x => x.Quantity).SingleAsync());
        var rows = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        Assert.All(rows, row =>
        {
            Assert.StartsWith("[ÖRNEK]", row.ItemName);
            Assert.StartsWith("[ÖRNEK]", row.EmployeeName);
            Assert.StartsWith("[ÖRNEK]", row.LocationName);
        });
        var items = await new GetStockItemsHandler(f.Db, f.User).Handle(new(), default);
        Assert.All(items, item => Assert.True(item.AvailableQuantity >= 0));
        Assert.Equal(0, items.Single(x => x.Code == "ORNEK-ZIM-PIL").AssignedQuantity);
    }

    [Fact]
    public async Task Restart_preserves_personnel_transfer_returns_edits_and_deleted_examples()
    {
        await using var f = await Fixture.Create();
        await f.Seed();
        var rows = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        var laptop = rows.Single(x => x.ItemCode == "ORNEK-ZIM-LAPTOP");
        var otherEmployee = await f.Db.Employees.SingleAsync(x => x.EmployeeNumber == "ORNEK-ZIM-002");
        var transferred = await new AssignStockCustodyHandler(f.Db, f.User).Handle(new()
        {
            Id = Guid.NewGuid(), EmployeeId = otherEmployee.Id, StockItemId = laptop.StockItemId,
            LocationId = laptop.LocationId, Quantity = laptop.Quantity, SerialNumber = laptop.SerialNumber,
            AssignedOn = laptop.AssignedOn, ReplacesId = laptop.Id, Notes = "Kullanıcı personeli değiştirdi"
        }, default);
        var radio = rows.Single(x => x.ItemCode == "ORNEK-ZIM-TELSIZ");
        await new ReturnStockCustodyHandler(f.Db, f.User).Handle(new()
        {
            Id = radio.Id, ReturnedOn = radio.AssignedOn, Notes = "Örnek iade"
        }, default);
        var consumable = await f.Db.StockItems.SingleAsync(x => x.Code == "ORNEK-ZIM-PIL");
        consumable.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await f.Seed();
        var active = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        Assert.Equal(2, active.Count);
        var newLaptop = active.Single(x => x.ItemCode == "ORNEK-ZIM-LAPTOP");
        Assert.Equal(transferred.Id, newLaptop.Id);
        Assert.Equal(otherEmployee.Id, newLaptop.EmployeeId);
        Assert.Equal(laptop.StockItemId, newLaptop.StockItemId);
        Assert.Equal(laptop.SerialNumber, newLaptop.SerialNumber);
        Assert.Equal(laptop.Quantity, newLaptop.Quantity);
        Assert.Equal(laptop.LocationId, newLaptop.LocationId);
        Assert.Equal("Kullanıcı personeli değiştirdi", newLaptop.Notes);
        Assert.True((await f.Db.StockItems.IgnoreQueryFilters().SingleAsync(x => x.Id == consumable.Id)).IsDeleted);
        Assert.Equal(4, await f.Db.StockCustodies.CountAsync());
        var history = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(IncludeReturned: true), default);
        Assert.Contains("Personel devri:", history.Single(x => x.Id == laptop.Id).ReturnNotes);
    }

    [Fact]
    public async Task Existing_operator_code_is_preserved_and_partial_examples_are_not_created()
    {
        await using var f = await Fixture.Create();
        f.RealItem.Code = "ORNEK-ZIM-LAPTOP";
        await f.Db.SaveChangesAsync();
        await f.Seed();
        Assert.Single(await f.Db.StockItems.ToListAsync());
        Assert.Empty(await f.Db.StockCustodies.ToListAsync());
        Assert.False(await f.Db.AppSettings.AnyAsync(x => x.Key == RequestedCustodySamples.MarkerKey));
        Assert.Equal("Gerçek ürün", (await f.Db.StockItems.SingleAsync()).Name);
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "sample-test";
        public bool IsAdmin { get; init; } = true;
        public bool IsInRole(string code) => IsAdmin && code == RoleCodes.SystemAdmin;
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.StockView, PermissionCodes.StockManage, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => Permissions.Contains(code);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public User User { get; } = new();
        public StockItem RealItem { get; } = new() { Name = "Gerçek ürün", Code = "REAL", Kind = StockItemKind.FixedAsset };
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            var root = new OrganizationUnit { Code = "KSSIM", Name = "Gerçek müdürlük", Type = OrganizationUnitType.Directorate };
            var depot = new OrganizationUnit { Code = "REAL_DEPOT", Name = "Gerçek depo", Type = OrganizationUnitType.Facility };
            f.Db.AddRange(root, depot, f.RealItem, new Employee { FirstName = "Gerçek", LastName = "Personel" });
            f.Db.StockBalances.Add(new() { StockItemId = f.RealItem.Id, LocationId = depot.Id, Quantity = 7 });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public Task Seed() => RequestedCustodySamples.SeedAsync(Db, NullLogger.Instance);
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
