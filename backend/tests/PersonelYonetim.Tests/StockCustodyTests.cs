using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Tests;

public sealed class StockCustodyTests
{
    [Fact]
    public async Task Assignment_reserves_stock_and_return_releases_it_without_inflating_total()
    {
        await using var f = await Fixture.Create();
        var row = await f.Assign(3);
        var item = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, item.TotalQuantity);
        Assert.Equal(3, item.AssignedQuantity);
        Assert.Equal(2, item.AvailableQuantity);
        var list = await new GetStockItemsHandler(f.Db, f.User).Handle(new(), default);
        Assert.Equal(3, Assert.Single(list).Locations.Single().AssignedQuantity);
        var location = await new GetStockLocationHandler(f.Db, f.User).Handle(new(f.Location.Id), default);
        Assert.Equal(2, Assert.Single(location.Lines).AvailableQuantity);
        var command = new ReturnStockCustodyCommand { Id = row.Id, ReturnedOn = Fixture.Today, Notes = "Teslim alındı" };
        await new ReturnStockCustodyHandler(f.Db, f.User).Handle(command, default);
        await new ReturnStockCustodyHandler(f.Db, f.User).Handle(command, default);
        item = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, item.AvailableQuantity);
        Assert.Equal(5, item.TotalQuantity);
        Assert.Empty(await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default));
        Assert.Single(await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(IncludeReturned: true), default));
    }

    [Fact]
    public async Task Insufficient_stock_and_duplicate_serial_are_rejected()
    {
        await using var f = await Fixture.Create();
        await f.Assign(1, "SERIAL-1");
        await Assert.ThrowsAsync<ConflictException>(() => f.Assign(5));
        await Assert.ThrowsAsync<ConflictException>(() => f.Assign(1, " serial-1 "));
        Assert.Equal(1, await f.Db.StockCustodies.CountAsync());
    }

    [Theory]
    [InlineData(StockItemKind.Consumable)]
    [InlineData(StockItemKind.Unspecified)]
    public async Task Only_explicit_fixed_assets_can_be_assigned(StockItemKind kind)
    {
        await using var f = await Fixture.Create();
        f.Item.Kind = kind;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => f.Assign(1));
    }

    [Fact]
    public async Task Replacement_preserves_history_and_releases_old_reservation_atomically()
    {
        await using var f = await Fixture.Create();
        var old = await f.Assign(5, notes: "İlk teslim");
        var command = f.Command(4);
        command.ReplacesId = old.Id;
        command.Notes = "Miktar değişti";
        var replacement = await new AssignStockCustodyHandler(f.Db, f.User).Handle(command, default);
        Assert.Equal(old.Id, replacement.ReplacesId);
        var history = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(IncludeReturned: true), default);
        Assert.Equal(2, history.Count);
        Assert.Equal("İlk teslim", history.Single(x => x.Id == old.Id).Notes);
        Assert.NotNull(history.Single(x => x.Id == old.Id).ReturnedOn);
        Assert.Equal(4, history.Single(x => x.ReturnedOn == null).Quantity);
        var detail = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(1, detail.AvailableQuantity);
    }

    [Fact]
    public async Task Failed_replacement_rolls_back_the_old_return()
    {
        await using var f = await Fixture.Create();
        var old = await f.Assign(5);
        var command = f.Command(6);
        command.ReplacesId = old.Id;
        await Assert.ThrowsAsync<ConflictException>(() => new AssignStockCustodyHandler(f.Db, f.User).Handle(command, default));
        f.Db.ChangeTracker.Clear();
        var persisted = await f.Db.StockCustodies.SingleAsync();
        Assert.Null(persisted.ReturnedOn);
        Assert.Equal(5, persisted.Quantity);
    }

    [Theory]
    [InlineData(StockMovementType.Outbound, 3)]
    [InlineData(StockMovementType.Transfer, 3)]
    [InlineData(StockMovementType.Adjustment, 2)]
    public async Task Stock_movements_cannot_consume_reserved_quantities(StockMovementType type, int quantity)
    {
        await using var f = await Fixture.Create();
        await f.Assign(3);
        var command = new CreateStockMovementCommand
        {
            StockItemId = f.Item.Id, MovementType = type, Quantity = quantity,
            FromLocationId = f.Location.Id,
            ToLocationId = type == StockMovementType.Adjustment ? f.Location.Id : f.OtherLocation.Id
        };
        await Assert.ThrowsAnyAsync<AppException>(() => new CreateStockMovementHandler(f.Db, f.User).Handle(command, default));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(5, await f.Db.StockBalances.Where(x => x.LocationId == f.Location.Id).Select(x => x.Quantity).SingleAsync());
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task Unreserved_stock_can_be_transferred_and_assigned_item_cannot_be_archived_or_reclassified()
    {
        await using var f = await Fixture.Create();
        await f.Assign(3);
        await new CreateStockMovementHandler(f.Db, f.User).Handle(new()
        {
            StockItemId = f.Item.Id, MovementType = StockMovementType.Transfer, Quantity = 2,
            FromLocationId = f.Location.Id, ToLocationId = f.OtherLocation.Id
        }, default);
        Assert.Equal(3, await f.Db.StockBalances.Where(x => x.LocationId == f.Location.Id).Select(x => x.Quantity).SingleAsync());
        await Assert.ThrowsAsync<ConflictException>(() => new ArchiveStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default));
        await Assert.ThrowsAsync<ConflictException>(() => new UpsertStockItemHandler(f.Db, f.User).Handle(new()
        {
            Id = f.Item.Id, Name = "Mikrofon", Kind = StockItemKind.Consumable, Unit = StockUnit.Piece
        }, default));
    }

    [Fact]
    public async Task Same_request_key_does_not_assign_twice_and_cannot_be_reused_with_changed_payload()
    {
        await using var f = await Fixture.Create();
        var request = f.Command(2);
        var handler = new AssignStockCustodyHandler(f.Db, f.User);
        await handler.Handle(request, default);
        await handler.Handle(request, default);
        Assert.Single(await f.Db.StockCustodies.ToListAsync());
        request.Quantity = 3;
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(request, default));
    }

    [Fact]
    public async Task View_only_user_cannot_assign_or_return_and_unscoped_user_sees_no_custody()
    {
        await using var f = await Fixture.Create();
        var old = await f.Assign(1);
        var viewer = new User(PermissionCodes.StockView, PermissionCodes.EmployeesViewAllUnits);
        await Assert.ThrowsAsync<ForbiddenException>(() => new AssignStockCustodyHandler(f.Db, viewer).Handle(f.Command(1), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ReturnStockCustodyHandler(f.Db, viewer).Handle(new() { Id = old.Id, ReturnedOn = Fixture.Today }, default));
        var noUnit = new User(PermissionCodes.StockView, PermissionCodes.StockManage);
        Assert.Empty(await new GetStockCustodiesHandler(f.Db, noUnit).Handle(new(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new AssignStockCustodyHandler(f.Db, noUnit).Handle(f.Command(1), default));
    }

    [Fact]
    public async Task Inactive_personnel_are_not_assignable_but_existing_custody_still_returns_after_departure()
    {
        await using var f = await Fixture.Create();
        var row = await f.Assign(1);
        f.Db.ChangeTracker.Clear();
        var employee = await f.Db.Employees.SingleAsync();
        employee.Status = EmployeeStatus.LeftJob;
        employee.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Assign(1));
        var history = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        Assert.Equal("Test Personel", Assert.Single(history).EmployeeName);
        await new ReturnStockCustodyHandler(f.Db, f.User).Handle(new() { Id = row.Id, ReturnedOn = Fixture.Today }, default);
    }

    [Fact]
    public async Task Custody_can_be_transferred_to_another_employee_without_consuming_more_stock()
    {
        await using var f = await Fixture.Create();
        var original = await f.Assign(5);
        var person = new Employee { FirstName = "İkinci", LastName = "Personel" };
        f.Db.Employees.Add(person);
        await f.Db.SaveChangesAsync();
        var command = f.Command(5);
        command.EmployeeId = person.Id;
        command.ReplacesId = original.Id;
        var assigned = await new AssignStockCustodyHandler(f.Db, f.User).Handle(command, default);
        Assert.Equal(person.Id, assigned.EmployeeId);
        var active = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        Assert.Equal(person.Id, Assert.Single(active).EmployeeId);
        var item = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, item.TotalQuantity);
        Assert.Equal(0, item.AvailableQuantity);
    }

    [Fact]
    public async Task Scoped_manager_cannot_assign_to_personnel_outside_their_unit()
    {
        await using var f = await Fixture.Create();
        var manager = new Employee { FirstName = "Birim", LastName = "Amiri", UnitId = f.Location.Id };
        var account = new AppUser { UserName = "manager", EmployeeId = manager.Id, Employee = manager };
        f.Db.Users.Add(account);
        await f.Db.SaveChangesAsync();
        var scopedUser = new User(PermissionCodes.StockView, PermissionCodes.StockManage) { UserId = account.Id };
        // The stock location is allowed, but the target employee is not in that unit.
        await Assert.ThrowsAsync<ForbiddenException>(() => new AssignStockCustodyHandler(f.Db, scopedUser).Handle(f.Command(1), default));
        var options = await new GetCustodyEmployeesHandler(f.Db, scopedUser).Handle(new(), default);
        Assert.Equal(manager.Id, Assert.Single(options).Id);
    }

    [Fact]
    public void Validators_reject_fractional_precision_serial_bulk_and_future_dates()
    {
        var validator = new AssignStockCustodyValidator();
        var request = new AssignStockCustodyCommand
        {
            Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), StockItemId = Guid.NewGuid(), LocationId = Guid.NewGuid(),
            AssignedOn = Fixture.Today, Quantity = 1
        };
        Assert.True(validator.Validate(request).IsValid);
        request.Quantity = .001m;
        Assert.False(validator.Validate(request).IsValid);
        request.Quantity = 2;
        request.SerialNumber = "ABC";
        Assert.False(validator.Validate(request).IsValid);
        request.Quantity = 1;
        request.AssignedOn = Fixture.Today.AddDays(1);
        Assert.False(validator.Validate(request).IsValid);
        var movement = new CreateStockMovementValidator().Validate(new CreateStockMovementCommand()
        {
            StockItemId = request.StockItemId, MovementType = StockMovementType.Inbound,
            ToLocationId = request.LocationId, Quantity = .001m
        });
        Assert.False(movement.IsValid);
    }

    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId { get; init; }
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool HasPermission(string code) => permissions.Contains(code);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public User User { get; } = new(PermissionCodes.StockView, PermissionCodes.StockManage, PermissionCodes.EmployeesViewAllUnits);
        public StockItem Item { get; } = new() { Name = "Mikrofon", Kind = StockItemKind.FixedAsset };
        public OrganizationUnit Location { get; } = new() { Name = "Salon", Type = OrganizationUnitType.Facility };
        public OrganizationUnit OtherLocation { get; } = new() { Name = "Diğer salon", Type = OrganizationUnitType.Facility };
        private readonly Employee employee = new() { FirstName = "Test", LastName = "Personel" };

        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.AddRange(f.Item, f.Location, f.OtherLocation, f.employee);
            f.Db.StockBalances.Add(new() { StockItemId = f.Item.Id, LocationId = f.Location.Id, Quantity = 5 });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public AssignStockCustodyCommand Command(decimal quantity) => new()
        {
            Id = Guid.NewGuid(), StockItemId = Item.Id, LocationId = Location.Id, EmployeeId = employee.Id,
            Quantity = quantity, AssignedOn = Today
        };
        public Task<StockCustodyDto> Assign(decimal quantity, string? serial = null, string? notes = null)
        {
            var command = Command(quantity);
            command.SerialNumber = serial;
            command.Notes = notes;
            return new AssignStockCustodyHandler(Db, User).Handle(command, default);
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
