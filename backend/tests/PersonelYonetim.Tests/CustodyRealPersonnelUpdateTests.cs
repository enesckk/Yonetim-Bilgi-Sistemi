using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class CustodyRealPersonnelUpdateTests
{
    [Fact]
    public async Task Legacy_examples_move_to_existing_people_and_facilities_without_increasing_stock()
    {
        await using var f = await Fixture.Create();
        await RequestedCustodySamples.SeedAsync(f.Db, NullLogger.Instance);
        var totalBefore = await f.Db.StockBalances.SumAsync(x => x.Quantity);
        await f.Update();
        f.Db.ChangeTracker.Clear();
        var active = await f.Db.StockCustodies.IgnoreQueryFilters().Include(x => x.Employee).Include(x => x.StockItem).ToListAsync();
        Assert.Equal(6, active.Count);
        var current = active.Where(x => x.ReturnedOn == null).ToList();
        Assert.Equal(3, current.Count);
        Assert.Equal(3, current.Select(x => x.EmployeeId).Distinct().Count());
        Assert.All(current, row =>
        {
            Assert.DoesNotContain("[ÖRNEK]", row.Employee.FullName);
            Assert.DoesNotContain("[ÖRNEK]", row.StockItem.Name);
            Assert.Equal(f.Facility.Id, row.LocationId);
            Assert.NotNull(row.ReplacesId);
        });
        Assert.Equal("Ahmet Oral", current.Single(x => x.StockItem.Code == "ORNEK-ZIM-LAPTOP").Employee.FullName);
        Assert.Equal("Abdullah Orhan", current.Single(x => x.StockItem.Code == "ORNEK-ZIM-TELSIZ").Employee.FullName);
        Assert.Equal("Ali Döner", current.Single(x => x.StockItem.Code == "ORNEK-ZIM-KULAKLIK").Employee.FullName);
        Assert.Equal(totalBefore, await f.Db.StockBalances.SumAsync(x => x.Quantity));
        Assert.Equal(3, await f.Db.Employees.CountAsync());
        Assert.False(await f.Db.OrganizationUnits.AnyAsync(x => x.Code == "ORNEK_ZIMMET_DEPO"));
        Assert.All(await f.Db.StockBalances.Where(x => x.LocationId == f.Facility.Id).ToListAsync(), balance => Assert.True(balance.Quantity > 0));
        Assert.Equal(3, current.Select(x => x.StockItemId).Distinct().Count());
    }

    [Fact]
    public async Task Fresh_setup_uses_only_existing_people_and_does_not_create_placeholders()
    {
        await using var f = await Fixture.Create();
        await f.Update();
        await f.Update();
        Assert.Equal(3, await f.Db.Employees.CountAsync());
        Assert.Equal(4, await f.Db.StockItems.CountAsync());
        Assert.Equal(3, await f.Db.StockCustodies.CountAsync());
        Assert.Equal(19, await f.Db.StockBalances.SumAsync(x => x.Quantity));
        Assert.Equal(2, await f.Db.OrganizationUnits.CountAsync());
    }

    [Fact]
    public async Task Returned_and_already_transferred_records_are_not_reset_on_restart()
    {
        await using var f = await Fixture.Create();
        await RequestedCustodySamples.SeedAsync(f.Db, NullLogger.Instance);
        var original = await f.Db.StockCustodies.Include(x => x.StockItem).SingleAsync(x => x.StockItem.Code == "ORNEK-ZIM-LAPTOP");
        original.ReturnedOn = original.AssignedOn;
        original.ReturnNotes = "Kullanıcı iade aldı";
        original.UpdatedBy = "operator";
        await f.Db.SaveChangesAsync();
        await f.Update();
        f.Db.ChangeTracker.Clear();
        Assert.False(await f.Db.StockCustodies.AnyAsync(x => x.StockItemId == original.StockItemId && x.ReturnedOn == null));
        var saved = await f.Db.StockCustodies.SingleAsync(x => x.Id == original.Id);
        Assert.Equal("Kullanıcı iade aldı", saved.ReturnNotes);
        var transfer = await f.Db.StockCustodies.FirstAsync(x => x.ReturnedOn == null);
        transfer.Notes = "Operatör açıklaması";
        await f.Db.SaveChangesAsync();
        await f.Update();
        Assert.Equal("Operatör açıklaması", (await f.Db.StockCustodies.SingleAsync(x => x.Id == transfer.Id)).Notes);
    }

    [Fact]
    public async Task No_existing_employee_means_no_invented_people_or_stock()
    {
        await using var f = await Fixture.Create(withPeople: false);
        await f.Update();
        Assert.Empty(await f.Db.Employees.ToListAsync());
        Assert.Empty(await f.Db.StockItems.ToListAsync());
        Assert.False(await f.Db.AppSettings.AnyAsync(x => x.Key == CustodyRealPersonnelUpdate.Marker));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public OrganizationUnit Facility { get; } = new() { Code = "KKM", Name = "Kültür Merkezi", Type = OrganizationUnitType.Facility };
        public static async Task<Fixture> Create(bool withPeople = true)
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            var root = new OrganizationUnit { Code = "KSSIM", Name = "Müdürlük", Type = OrganizationUnitType.Directorate };
            f.Facility.ParentId = root.Id;
            f.Db.AddRange(root, f.Facility);
            if (withPeople)
                foreach (var (first, last) in new[] { ("Ahmet", "Oral"), ("Abdullah", "Orhan"), ("Ali", "Döner") })
                    f.Db.Employees.Add(new() { FirstName = first, LastName = last, FacilityId = f.Facility.Id, UnitId = f.Facility.Id });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public Task Update() => CustodyRealPersonnelUpdate.SeedAsync(Db, NullLogger.Instance);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
