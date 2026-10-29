using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Tests;

public class EventMediaTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFo0AAAAASUVORK5CYII=");
    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "media-test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool HasPermission(string code) => permissions.Contains(code);
    }
    private static readonly User Director = new(PermissionCodes.EventsView, PermissionCodes.EventsManage, PermissionCodes.EmployeesViewAllUnits);
    [Fact]
    public async Task Poster_replacement_and_gallery_batches_are_atomic_and_preserve_the_event()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        await db.Database.EnsureCreatedAsync();
        var e = new Event { Title = "Festival", Requirements = "2 mikrofon\n100 kişilik ikram", Description = "Existing notes", StartAtUtc = DateTime.UtcNow };
        db.Events.Add(e); await db.SaveChangesAsync();
        var service = new EventMediaService(db, Director);
        await service.SaveAsync(e.Id, "poster", [new("poster.png", Png)]);
        var poster = Assert.Single(await service.ListAsync(e.Id)); Assert.Equal(e.Id, poster.Id);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(e.Id, "poster", [new("bad.png", "invalid"u8.ToArray())]));
        Assert.Equal(Png, (await service.ReadAsync(e.Id, poster.Id)).Data);
        await service.SaveAsync(e.Id, "poster", [new("replacement.png", Png)]);
        Assert.Equal("replacement.png", Assert.Single(await service.ListAsync(e.Id)).FileName);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(e.Id, "photo", [new("good.png", Png), new("bad.svg", "<svg/>"u8.ToArray())]));
        Assert.Single(await service.ListAsync(e.Id));
        await service.SaveAsync(e.Id, "photo", Enumerable.Range(0, 20).Select(i => new EventMediaUpload($"photo{i}.png", Png)).ToList());
        Assert.Equal(21, (await service.ListAsync(e.Id)).Count);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(e.Id, "photo", [new("overflow.png", Png)]));
        var photo = (await service.ListAsync(e.Id)).First(x => x.Kind == "photo");
        await service.DeleteAsync(e.Id, photo.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReadAsync(e.Id, photo.Id));
        Assert.Empty((await db.EventMedia.IgnoreQueryFilters().SingleAsync(x => x.Id == photo.Id)).FileData);
        await service.DeleteAsync(e.Id, poster.Id);
        await service.SaveAsync(e.Id, "poster", [new("restored.png", Png)]);
        Assert.Single(await service.ListAsync(e.Id), x => x.Kind == "poster");
        Assert.Equal("2 mikrofon\n100 kişilik ikram", (await db.Events.SingleAsync()).Requirements);
        Assert.Equal("Existing notes", (await db.Events.SingleAsync()).Description);
    }
    [Fact]
    public async Task Media_requires_event_permission_and_the_same_unit_scope_as_event_details()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options); await db.Database.EnsureCreatedAsync();
        var e = new Event { Title = "Private", StartAtUtc = DateTime.UtcNow }; db.Events.Add(e); await db.SaveChangesAsync();
        var service = new EventMediaService(db, Director); await service.SaveAsync(e.Id, "poster", [new("poster.png", Png)]);
        foreach (var user in new[] { new User(PermissionCodes.EmployeesViewAllUnits), new User(PermissionCodes.EventsView, PermissionCodes.EventsManage) })
        {
            var denied = new EventMediaService(db, user);
            await Assert.ThrowsAsync<ForbiddenException>(() => denied.ListAsync(e.Id));
            await Assert.ThrowsAsync<ForbiddenException>(() => denied.ReadAsync(e.Id, e.Id));
            await Assert.ThrowsAsync<ForbiddenException>(() => denied.SaveAsync(e.Id, "poster", [new("test.png", Png)]));
            await Assert.ThrowsAsync<ForbiddenException>(() => denied.DeleteAsync(e.Id, e.Id));
        }
        var viewer = new EventMediaService(db, new User(PermissionCodes.EventsView, PermissionCodes.EmployeesViewAllUnits));
        Assert.Equal(Png, (await viewer.ReadAsync(e.Id, e.Id)).Data);
        await Assert.ThrowsAsync<ForbiddenException>(() => viewer.DeleteAsync(e.Id, e.Id));
        var other = new Event { Title = "Other", StartAtUtc = DateTime.UtcNow }; db.Events.Add(other); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReadAsync(other.Id, e.Id));
        e.IsDeleted = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListAsync(e.Id));
    }
    [Fact]
    public async Task Requirements_survive_legacy_updates_and_can_be_explicitly_cleared()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options); await db.Database.EnsureCreatedAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var create = new CreateEventHandler(db, Director, null!, cache);
        var id = await create.Handle(new CreateEventCommand { Title = "Plan", Requirements = "Ses sistemi", Status = EventStatus.Draft, StartAtUtc = DateTime.UtcNow }, default);
        var update = new UpdateEventHandler(db, Director, cache);
        var command = new UpdateEventCommand { Id = id, Title = "Edited", Status = EventStatus.Draft, StartAtUtc = DateTime.UtcNow };
        await update.Handle(command, default);
        Assert.Equal("Ses sistemi", (await new GetEventByIdHandler(db, Director).Handle(new(id), default))!.Requirements);
        command.Requirements = ""; await update.Handle(command, default);
        Assert.Equal("", (await db.Events.SingleAsync()).Requirements);
        Assert.False((await new UpdateEventCommandValidator().ValidateAsync(new UpdateEventCommand { Requirements = new string('x', 8001) })).IsValid);
    }
    [Fact]
    public void File_validation_rejects_wrong_extension_content_empty_and_oversized_data()
    {
        foreach (var file in new[] { new EventMediaUpload("test.svg", Png), new("test.png", []), new("test.png", "bad"u8.ToArray()), new("test.png", new byte[EventMediaService.MaxBytes + 1]) })
            Assert.Throws<ValidationException>(() => EventMediaService.Validate(file));
        Assert.Equal("image/png", EventMediaService.Validate(new("valid.PNG", Png)));
    }
}
