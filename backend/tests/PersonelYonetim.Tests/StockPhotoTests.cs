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

public sealed class StockPhotoTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFo0AAAAASUVORK5CYII=");

    [Fact]
    public async Task Admin_can_add_replace_and_remove_photo_without_changing_inventory_or_custody()
    {
        await using var f = await Fixture.Create();
        var command = new AssignStockCustodyCommand { Id = Guid.NewGuid(), StockItemId = f.Item.Id, EmployeeId = f.Employee.Id, LocationId = f.Location.Id, Quantity = 2, AssignedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)) };
        await new AssignStockCustodyHandler(f.Db, f.Admin).Handle(command, default);
        var upload = new UploadStockPhotoHandler(f.Db, f.Admin, f.Files);
        await upload.Handle(new(f.Item.Id, new MemoryStream(Png), "image.PNG", "application/octet-stream"), default);
        var firstPath = (await f.Db.StockItems.SingleAsync()).PhotoPath;
        Assert.NotNull(firstPath);
        var row = Assert.Single(await new GetStockItemsHandler(f.Db, f.Admin).Handle(new(), default));
        Assert.True(row.HasPhoto);
        Assert.NotNull(row.PhotoVersion);
        var custody = Assert.Single(await new GetStockCustodiesHandler(f.Db, f.Admin).Handle(new(), default));
        Assert.True(custody.HasPhoto);
        Assert.Equal(2, custody.RemainingQuantity);
        await upload.Handle(new(f.Item.Id, new MemoryStream(Png), "replacement.png", "image/png"), default);
        var nextPath = (await f.Db.StockItems.SingleAsync()).PhotoPath;
        Assert.NotEqual(firstPath, nextPath);
        Assert.False(f.Files.Data.ContainsKey(firstPath!));
        var director = new User(false);
        await using (var photo = await new OpenStockPhotoHandler(f.Db, director, f.Files).Handle(new(f.Item.Id), default))
        {
            var bytes = new MemoryStream(); await photo.Stream.CopyToAsync(bytes);
            Assert.Equal(Png, bytes.ToArray()); Assert.Equal("image/png", photo.ContentType);
        }
        await new DeleteStockPhotoHandler(f.Db, f.Admin, f.Files).Handle(new(f.Item.Id), default);
        Assert.Null((await f.Db.StockItems.SingleAsync()).PhotoPath);
        Assert.Empty(f.Files.Data);
        var detail = await new GetStockItemHandler(f.Db, f.Admin).Handle(new(f.Item.Id), default);
        Assert.Equal(5, detail.TotalQuantity); Assert.Equal(2, detail.AssignedQuantity); Assert.Equal(3, detail.AvailableQuantity);
        Assert.Single(await f.Db.StockCustodies.ToListAsync());
    }

    [Fact]
    public async Task Director_with_stale_write_permissions_cannot_upload_or_remove_photo()
    {
        await using var f = await Fixture.Create();
        var director = new User(false);
        await Assert.ThrowsAsync<ForbiddenException>(() => new UploadStockPhotoHandler(f.Db, director, f.Files).Handle(new(f.Item.Id, new MemoryStream(Png), "image.png", "image/png"), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new DeleteStockPhotoHandler(f.Db, director, f.Files).Handle(new(f.Item.Id), default));
        Assert.Empty(f.Files.Data);
        Assert.Null((await f.Db.StockItems.SingleAsync()).PhotoPath);
    }

    [Theory]
    [InlineData("image.svg", "<svg></svg>")]
    [InlineData("image.png", "not an image")]
    [InlineData("image.jpg", "not an image")]
    public async Task Invalid_images_are_rejected_before_storage(string fileName, string content)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ValidationException>(() => new UploadStockPhotoHandler(f.Db, f.Admin, f.Files).Handle(new(f.Item.Id, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), fileName, "image/png"), default));
        Assert.Empty(f.Files.Data);
    }

    [Fact]
    public async Task Oversize_upload_and_failed_replacement_preserve_the_existing_photo()
    {
        await using var f = await Fixture.Create();
        var handler = new UploadStockPhotoHandler(f.Db, f.Admin, f.Files);
        await handler.Handle(new(f.Item.Id, new MemoryStream(Png), "good.png", "image/png"), default);
        var path = (await f.Db.StockItems.SingleAsync()).PhotoPath!;
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new(f.Item.Id, new MemoryStream(new byte[5 * 1024 * 1024 + 1]), "big.png", "image/png"), default));
        f.Files.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(f.Item.Id, new MemoryStream(Png), "new.png", "image/png"), default));
        Assert.Equal(path, (await f.Db.StockItems.SingleAsync()).PhotoPath);
        Assert.Equal(Png, f.Files.Data[path]);
    }

    private sealed class User(bool admin = true) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => admin ? "admin" : "director";
        public bool IsAuthenticated => true;
        public bool IsInRole(string code) => code == (admin ? RoleCodes.SystemAdmin : RoleCodes.Director);
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.StockView, PermissionCodes.StockManage, PermissionCodes.StockCustodyManage, PermissionCodes.FilesUpload, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => Permissions.Contains(code);
    }

    private sealed class Files : IFileStorageService
    {
        public Dictionary<string, byte[]> Data { get; } = [];
        public bool FailSave { get; set; }
        public async Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, string relativeFolder, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("Storage unavailable");
            var bytes = new MemoryStream(); await content.CopyToAsync(bytes, cancellationToken);
            var path = relativeFolder + "/" + Guid.NewGuid() + ".png";
            Data[path] = bytes.ToArray();
            return new() { RelativePath = path, Extension = ".png", SizeBytes = bytes.Length };
        }
        public Task<StoredFileOpenResult?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(Data.TryGetValue(relativePath, out var bytes) ? new StoredFileOpenResult { Stream = new MemoryStream(bytes), ContentType = "image/png", DownloadFileName = "image.png" } : null);
        public Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default) { if (relativePath is not null) Data.Remove(relativePath); return Task.CompletedTask; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public StockItem Item { get; } = new() { Name = "Demirbaş", Kind = StockItemKind.FixedAsset };
        public Employee Employee { get; } = new() { FirstName = "Test", LastName = "Personel" };
        public OrganizationUnit Location { get; } = new() { Name = "Tesis", Type = OrganizationUnitType.Facility };
        public User Admin { get; } = new();
        public Files Files { get; } = new();
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.AddRange(f.Item, f.Employee, f.Location);
            f.Db.StockBalances.Add(new() { StockItemId = f.Item.Id, LocationId = f.Location.Id, Quantity = 5 });
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
