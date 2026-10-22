using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Logging.Abstractions;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Tests;

/// <summary>Opt-in integration tests using disposable databases on a local PostgreSQL instance.</summary>
public class PostgresStockCustodyTests
{
    [LocalPostgresFact]
    public async Task Requested_samples_are_added_once_even_when_two_instances_start_together()
    {
        await using var f = await Fixture.Create();
        await using (var setup = f.Context())
        {
            setup.OrganizationUnits.Add(new() { Code = "KSSIM", Name = "Müdürlük", Type = OrganizationUnitType.Directorate });
            await setup.SaveChangesAsync();
        }
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = f.Context();
            await RequestedCustodySamples.SeedAsync(db, NullLogger.Instance);
        }));
        await using var check = f.Context();
        Assert.Equal(3, await check.StockCustodies.CountAsync());
        Assert.Equal(5, await check.StockItems.CountAsync());
        Assert.Equal(1, await check.AppSettings.CountAsync(x => x.Key == RequestedCustodySamples.MarkerKey));
        Assert.Equal(1, await check.StockBalances.Where(x => x.StockItemId == f.Item.Id).Select(x => x.Quantity).SingleAsync());
    }

    [LocalPostgresFact]
    public async Task Migration_preserves_existing_items_and_marks_kind_unspecified()
    {
        await using var f = await Fixture.Create(migrate: false);
        await using var db = f.Context();
        var migrator = db.GetService<IMigrator>();
        var baseline = db.Database.GetMigrations().First();
        await migrator.MigrateAsync(baseline);
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO personel_app."StockItems" ("Id", "Name", "Category", "Unit", "MinQuantity", "IsActive", "CreatedAtUtc", "IsDeleted")
            VALUES ({id}, 'Existing stock', 99, 1, 2, TRUE, CURRENT_TIMESTAMP, FALSE)
            """);
        await db.Database.MigrateAsync();
        var item = await db.StockItems.SingleAsync(x => x.Id == id);
        Assert.Equal("Existing stock", item.Name);
        Assert.Equal(2, item.MinQuantity);
        Assert.Equal(StockItemKind.Unspecified, item.Kind);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [LocalPostgresFact]
    public async Task Concurrent_assignments_cannot_allocate_the_same_last_item()
    {
        await using var f = await Fixture.Create();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var db = f.Context();
            try { await new AssignStockCustodyHandler(db, f.User).Handle(f.Assign(), default); return true; }
            catch (ConflictException) { return false; }
        }));
        Assert.Single(results, x => x);
        await using var check = f.Context();
        Assert.Single(await check.StockCustodies.Where(x => x.ReturnedOn == null).ToListAsync());
        Assert.Equal(1, await check.StockBalances.Select(x => x.Quantity).SingleAsync());
    }

    [LocalPostgresFact]
    public async Task Concurrent_outbound_and_assignment_preserve_available_stock()
    {
        await using var f = await Fixture.Create();
        async Task<bool> Assign()
        {
            await using var db = f.Context();
            try { await new AssignStockCustodyHandler(db, f.User).Handle(f.Assign(), default); return true; }
            catch (ConflictException) { return false; }
        }
        async Task<bool> Outbound()
        {
            await using var db = f.Context();
            try
            {
                await new CreateStockMovementHandler(db, f.User).Handle(new()
                {
                    StockItemId = f.Item.Id, FromLocationId = f.Location.Id,
                    Quantity = 1, MovementType = StockMovementType.Outbound
                }, default);
                return true;
            }
            catch (AppException) { return false; }
        }
        var results = await Task.WhenAll(Assign(), Outbound());
        Assert.Single(results, x => x);
        await using var check = f.Context();
        var detail = await new GetStockItemHandler(check, f.User).Handle(new(f.Item.Id), default);
        Assert.Equal(0, detail.AvailableQuantity);
        Assert.True(detail.TotalQuantity >= detail.AssignedQuantity);
    }

    [LocalPostgresFact]
    public async Task Simultaneous_duplicate_requests_create_only_one_record()
    {
        await using var f = await Fixture.Create();
        var command = f.Assign();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = f.Context();
            try { await new AssignStockCustodyHandler(db, f.User).Handle(command, default); }
            catch (ConflictException) { /* A concurrent request may win the unique key race. */ }
        }));
        await using var check = f.Context();
        Assert.Single(await check.StockCustodies.ToListAsync());
        await new AssignStockCustodyHandler(check, f.User).Handle(command, default);
    }

    [LocalPostgresFact]
    public async Task Replacement_failure_rolls_back_in_postgres_and_same_serial_can_be_returned_then_reissued()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Context();
        var original = f.Assign();
        original.SerialNumber = "ABC-1";
        await new AssignStockCustodyHandler(db, f.User).Handle(original, default);
        var replacement = f.Assign();
        replacement.ReplacesId = original.Id;
        replacement.Quantity = 2;
        await Assert.ThrowsAsync<ConflictException>(() => new AssignStockCustodyHandler(db, f.User).Handle(replacement, default));
        db.ChangeTracker.Clear();
        Assert.Null((await db.StockCustodies.SingleAsync()).ReturnedOn);
        replacement.Quantity = 1;
        replacement.SerialNumber = "ABC-1";
        await new AssignStockCustodyHandler(db, f.User).Handle(replacement, default);
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.StockCustodies.CountAsync());
        Assert.Single(await db.StockCustodies.Where(x => x.ReturnedOn == null).ToListAsync());
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "postgres-test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.StockView, PermissionCodes.StockManage, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => Permissions.Contains(code);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string adminConnection;
        private readonly string database = "ybs_custody_test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        public User User { get; } = new();
        public StockItem Item { get; } = new() { Name = "Postgres test", Kind = StockItemKind.FixedAsset };
        public OrganizationUnit Location { get; } = new() { Name = "Test salon", Type = OrganizationUnitType.Facility };
        private readonly Employee employee = new() { FirstName = "Test", LastName = "Personel" };
        private Fixture()
        {
            var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("YBS_TEST_POSTGRES_CONNECTION"));
            if (cs.Host is not ("localhost" or "127.0.0.1") || cs.Database != "postgres")
                throw new InvalidOperationException("Integration tests require a disposable local PostgreSQL server and Database=postgres.");
            adminConnection = cs.ConnectionString;
            cs.Database = database;
            cs.Pooling = false;
            connection = cs.ConnectionString;
        }
        public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, pg =>
            {
                pg.MigrationsAssembly("PersonelYonetim.PostgresMigrations");
                pg.EnableRetryOnFailure(5, TimeSpan.FromMilliseconds(200), null);
            }).Options);
        public static async Task<Fixture> Create(bool migrate = true)
        {
            var f = new Fixture();
            await using var admin = new NpgsqlConnection(f.adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {f.database}", admin).ExecuteNonQueryAsync();
            if (migrate)
            {
                await using var db = f.Context();
                await db.Database.MigrateAsync();
                db.AddRange(f.Item, f.Location, f.employee);
                db.StockBalances.Add(new() { StockItemId = f.Item.Id, LocationId = f.Location.Id, Quantity = 1 });
                await db.SaveChangesAsync();
            }
            return f;
        }
        public AssignStockCustodyCommand Assign() => new()
        {
            Id = Guid.NewGuid(), StockItemId = Item.Id, LocationId = Location.Id,
            EmployeeId = employee.Id, Quantity = 1, AssignedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3))
        };
        public async ValueTask DisposeAsync()
        {
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}

public sealed class LocalPostgresFactAttribute : FactAttribute
{
    public LocalPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YBS_TEST_POSTGRES_CONNECTION")))
            Skip = "Set YBS_TEST_POSTGRES_CONNECTION to run against a disposable local PostgreSQL instance.";
    }
}
