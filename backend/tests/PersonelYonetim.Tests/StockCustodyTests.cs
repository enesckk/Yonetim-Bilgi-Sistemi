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
    public async Task Custody_manager_can_assign_and_return_but_cannot_edit_the_stock_catalog()
    {
        await using var f = await Fixture.Create();
        var director = new User(PermissionCodes.StockView, PermissionCodes.StockCustodyManage, PermissionCodes.EmployeesViewAllUnits);
        var row = await new AssignStockCustodyHandler(f.Db, director).Handle(f.Command(1), default);
        await new ReturnStockCustodyHandler(f.Db, director).Handle(new() { Id = row.Id, ReturnedOn = Fixture.Today }, default);
        await Assert.ThrowsAsync<ForbiddenException>(() => new UpsertStockItemHandler(f.Db, director).Handle(new()
        {
            Id = f.Item.Id, Name = "Unauthorized stock edit", Kind = StockItemKind.FixedAsset
        }, default));
    }

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

    [Fact]
    public async Task Partial_returns_preserve_original_quantity_and_release_only_the_returned_amount_once()
    {
        await using var f = await Fixture.Create();
        var original = await f.Assign(5);
        var request = new ReturnStockCustodyCommand { Id = original.Id, OperationId = Guid.NewGuid(), Quantity = 2, ReturnedOn = Fixture.Today, Notes = "İki adet" };
        var handler = new ReturnStockCustodyHandler(f.Db, f.User);
        var partial = await handler.Handle(request, default);
        await handler.Handle(request, default);
        Assert.Equal(5, partial.Quantity);
        Assert.Equal(2, partial.ReturnedQuantity);
        Assert.Equal(3, partial.RemainingQuantity);
        Assert.Null(partial.ReturnedOn);
        Assert.Single(partial.Returns);
        var detail = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, detail.TotalQuantity);
        Assert.Equal(2, detail.AvailableQuantity);
        Assert.Equal(3, detail.AssignedQuantity);
        var list = await new GetStockItemsHandler(f.Db, f.User).Handle(new(), default);
        Assert.Equal(3, Assert.Single(list).AssignedQuantity);
        var location = await new GetStockLocationHandler(f.Db, f.User).Handle(new(f.Location.Id), default);
        Assert.Equal(2, Assert.Single(location.Lines).AvailableQuantity);
        request.Quantity = 1;
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(request, default));
        var invalid = new ReturnStockCustodyCommand { Id = original.Id, OperationId = Guid.NewGuid(), Quantity = 4, ReturnedOn = Fixture.Today };
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(invalid, default));
        var final = await handler.Handle(new() { Id = original.Id, OperationId = Guid.NewGuid(), Quantity = 3, ReturnedOn = Fixture.Today }, default);
        Assert.Equal(0, final.RemainingQuantity);
        Assert.Equal(5, final.ReturnedQuantity);
        Assert.NotNull(final.ReturnedOn);
        Assert.Equal(2, final.Returns.Count);
        detail = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, detail.AvailableQuantity);
        Assert.Equal(5, detail.TotalQuantity);
    }

    [Fact]
    public async Task Partially_returned_custody_can_transfer_only_the_remaining_quantity()
    {
        await using var f = await Fixture.Create();
        var original = await f.Assign(5);
        await new ReturnStockCustodyHandler(f.Db, f.User).Handle(new() { Id = original.Id, OperationId = Guid.NewGuid(), Quantity = 2, ReturnedOn = Fixture.Today }, default);
        var person = new Employee { FirstName = "Yeni", LastName = "Personel" };
        f.Db.Employees.Add(person);
        await f.Db.SaveChangesAsync();
        var command = f.Command(3); command.ReplacesId = original.Id; command.EmployeeId = person.Id;
        var row = await new AssignStockCustodyHandler(f.Db, f.User).Handle(command, default);
        Assert.Equal(3, row.RemainingQuantity);
        var detail = await new GetStockItemHandler(f.Db, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(5, detail.TotalQuantity);
        Assert.Equal(2, detail.AvailableQuantity);
        var history = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(IncludeReturned: true), default);
        Assert.Single(history.Single(x => x.Id == original.Id).Returns);
    }

    [Fact]
    public async Task Registration_and_first_inbound_dates_are_distinct_and_unknown_inbound_is_not_invented()
    {
        await using var f = await Fixture.Create();
        await f.Assign(1);
        var handler = new GetStockCustodiesHandler(f.Db, f.User);
        var row = Assert.Single(await handler.Handle(new(), default));
        Assert.NotEqual(default, row.ItemCreatedAtUtc);
        Assert.Null(row.FirstStockEntryOn);
        f.Db.StockMovements.Add(new StockMovement { StockItemId = f.Item.Id, ToLocationId = f.Location.Id, MovementType = StockMovementType.Inbound, Quantity = 5, OccurredOn = Fixture.Today.AddDays(-10) });
        f.Db.StockMovements.Add(new StockMovement { StockItemId = f.Item.Id, ToLocationId = f.Location.Id, MovementType = StockMovementType.Inbound, Quantity = 1, OccurredOn = Fixture.Today.AddDays(-2) });
        await f.Db.SaveChangesAsync();
        row = Assert.Single(await handler.Handle(new(), default));
        Assert.Equal(Fixture.Today.AddDays(-10), row.FirstStockEntryOn);
    }

    [Theory]
    [InlineData(EmployeeStatus.Passive)]
    [InlineData(EmployeeStatus.LeftJob)]
    [InlineData(EmployeeStatus.Retired)]
    [InlineData(EmployeeStatus.TransferredToOtherDirectorate)]
    [InlineData(EmployeeStatus.DutyEnded)]
    public async Task Departure_requires_explicit_custody_warning_acknowledgment(EmployeeStatus status)
    {
        await using var f = await Fixture.Create();
        var custody = await f.Assign(1);
        var actor = new User(PermissionCodes.EmployeesSetStatus);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var handler = new PersonelYonetim.Infrastructure.Employees.SetEmployeeStatusHandler(f.Db, actor, null!, cache);
        var command = new PersonelYonetim.Application.Features.Employees.SetEmployeeStatusCommand { EmployeeId = custody.EmployeeId, Status = status, EffectiveDate = Fixture.Today, Reason = "Test" };
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, default));
        Assert.Equal("ACTIVE_CUSTODY_WARNING", error.Code);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(EmployeeStatus.Active, (await f.Db.Employees.SingleAsync()).Status);
        // Departure may continue after acknowledgment; no automatic return or stock change.
        if (status != EmployeeStatus.Passive)
        {
            command.AcknowledgeActiveCustodies = true;
            await handler.Handle(command, default);
            Assert.Single(await f.Db.StockCustodies.Where(x => x.ReturnedOn == null).ToListAsync());
            Assert.Equal(5, (await f.Db.StockBalances.SingleAsync()).Quantity);
        }
    }

    [Fact]
    public async Task Archiving_requires_acknowledgment_and_keeps_custody_history()
    {
        await using var f = await Fixture.Create();
        var custody = await f.Assign(1);
        var actor = new User(PermissionCodes.EmployeesArchive);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var handler = new PersonelYonetim.Infrastructure.Employees.ArchiveEmployeeHandler(f.Db, actor, cache);
        var command = new PersonelYonetim.Application.Features.Employees.ArchiveEmployeeCommand { EmployeeId = custody.EmployeeId, Reason = "Test" };
        Assert.Equal("ACTIVE_CUSTODY_WARNING", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, default))).Code);
        Assert.False((await f.Db.Employees.SingleAsync()).IsDeleted);
        command.AcknowledgeActiveCustodies = true;
        await handler.Handle(command, default);
        Assert.True((await f.Db.Employees.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        var rows = await new GetStockCustodiesHandler(f.Db, f.User).Handle(new(), default);
        Assert.Single(rows);
    }

    [Fact]
    public async Task Editing_employee_and_manual_directorate_transfer_cannot_bypass_the_custody_warning()
    {
        await using var f = await Fixture.Create();
        var custody = await f.Assign(1);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var actor = new User(PermissionCodes.EmployeesUpdate, PermissionCodes.EmployeesSetStatus, PermissionCodes.EmployeesViewAllUnits, PermissionCodes.MovementsCreate);
        var edit = new PersonelYonetim.Infrastructure.Employees.UpdateEmployeeHandler(f.Db, actor, null!, null!, cache);
        var error = await Assert.ThrowsAsync<AppException>(() => edit.Handle(new() { Id = custody.EmployeeId, FirstName = "Test", LastName = "Personel", Status = EmployeeStatus.LeftJob }, default));
        Assert.Equal("ACTIVE_CUSTODY_WARNING", error.Code);
        var movement = new PersonelYonetim.Infrastructure.Employees.CreateMovementHandler(f.Db, actor);
        var command = new PersonelYonetim.Application.Features.Employees.CreateMovementCommand { EmployeeId = custody.EmployeeId, MovementType = MovementType.TransferToOtherDirectorate, StartDate = Fixture.Today };
        error = await Assert.ThrowsAsync<AppException>(() => movement.Handle(command, default));
        Assert.Equal("ACTIVE_CUSTODY_WARNING", error.Code);
        Assert.Empty(await f.Db.EmployeeMovements.ToListAsync());
        command.AcknowledgeActiveCustodies = true;
        await movement.Handle(command, default);
        Assert.Equal(EmployeeStatus.TransferredToOtherDirectorate, (await f.Db.Employees.SingleAsync()).Status);
        Assert.Null((await f.Db.StockCustodies.SingleAsync()).ReturnedOn);
    }

    [Fact]
    public async Task Serial_numbered_assets_cannot_be_fractionally_returned()
    {
        await using var f = await Fixture.Create();
        var custody = await f.Assign(1, "TEKIL-001");
        var handler = new ReturnStockCustodyHandler(f.Db, f.User);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new() { Id = custody.Id, OperationId = Guid.NewGuid(), Quantity = .5m, ReturnedOn = Fixture.Today }, default));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.StockCustodyReturns.ToListAsync());
        Assert.Equal(0, (await f.Db.StockCustodies.SingleAsync()).ReturnedQuantity);
        var returned = await handler.Handle(new() { Id = custody.Id, OperationId = Guid.NewGuid(), Quantity = 1, ReturnedOn = Fixture.Today }, default);
        Assert.NotNull(returned.ReturnedOn);
        Assert.Equal(0, returned.RemainingQuantity);
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
