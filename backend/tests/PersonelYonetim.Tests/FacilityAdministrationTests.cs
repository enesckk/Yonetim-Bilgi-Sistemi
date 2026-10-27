using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Application.Features.Organization;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Events;
using PersonelYonetim.Infrastructure.Organization;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class FacilityAdministrationTests
{
    [Fact]
    public async Task Director_cannot_change_facilities_even_with_existing_organization_manage_permission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var directorate = new OrganizationUnit { Name = "Müdürlük", Type = OrganizationUnitType.Directorate };
        var site = new OrganizationUnit { Name = "Tesis", Type = OrganizationUnitType.Facility, ParentId = directorate.Id };
        db.AddRange(directorate, site); await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions()); var director = new User(false);
        await Assert.ThrowsAsync<ForbiddenException>(() => new CreateOrganizationUnitHandler(db, director, cache).Handle(new() { Name = "Yeni", Type = OrganizationUnitType.Facility, ParentId = directorate.Id }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new UpdateOrganizationUnitHandler(db, director, cache).Handle(new() { Id = site.Id, Name = "Değişti", Type = OrganizationUnitType.Facility, ParentId = directorate.Id }, default));
        // Changing the requested type must not bypass the restriction on an existing facility.
        await Assert.ThrowsAsync<ForbiddenException>(() => new UpdateOrganizationUnitHandler(db, director, cache).Handle(new() { Id = site.Id, Name = "Değişti", Type = OrganizationUnitType.MainUnit, ParentId = directorate.Id }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new DeleteOrganizationUnitHandler(db, director, cache).Handle(new(site.Id), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ImportFacilityCoordsHandler(db, director).Handle(new ImportFacilityCoordsCommand { FileStream = Stream.Null, FileName = "test.csv" }, default));
        Assert.Equal("Tesis", site.Name); Assert.False(site.IsDeleted);
        Assert.Equal(2, await db.OrganizationUnits.CountAsync());
    }

    [Fact]
    public async Task Admin_can_create_edit_delete_facility_and_director_keeps_other_unit_management()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var directorate = new OrganizationUnit { Name = "Müdürlük", Type = OrganizationUnitType.Directorate };
        var parent = new OrganizationUnit { Name = "Üst birim", Type = OrganizationUnitType.MainUnit, ParentId = directorate.Id };
        db.AddRange(directorate, parent); await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());var admin = new User(true);
        var id = await new CreateOrganizationUnitHandler(db, admin, cache).Handle(new() { Name = "Yeni tesis", Type = OrganizationUnitType.Facility, ParentId = parent.Id }, default);
        await new UpdateOrganizationUnitHandler(db, admin, cache).Handle(new() { Id = id, Name = "Güncel tesis", Type = OrganizationUnitType.Facility, ParentId = directorate.Id }, default);
        Assert.Equal("Güncel tesis", (await db.OrganizationUnits.SingleAsync(x => x.Id == id)).Name);
        await new DeleteOrganizationUnitHandler(db, admin, cache).Handle(new(id), default);
        Assert.False(await db.OrganizationUnits.AnyAsync(x => x.Id == id));
        await new CreateOrganizationUnitHandler(db, new User(false), cache).Handle(new() { Name = "Ana birim", Type = OrganizationUnitType.MainUnit, ParentId = directorate.Id }, default);
        Assert.True(await db.OrganizationUnits.AnyAsync(x => x.Name == "Ana birim"));
    }

    private sealed class User(bool admin) : ICurrentUserService
    {
        public Guid? UserId => null; public string? UserName => "test"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.OrganizationManage, PermissionCodes.OrganizationView, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => Permissions.Contains(code);
        public bool IsInRole(string code) => code == (admin ? RoleCodes.SystemAdmin : RoleCodes.Director);
    }
}
