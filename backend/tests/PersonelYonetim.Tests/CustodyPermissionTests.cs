using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Auth;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class CustodyPermissionTests
{
    [Fact]
    public async Task Upgrade_removes_director_write_but_preserves_view_and_does_not_regrant_revoked_admin_permission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var director = new Role { Name = "Müdür", Code = RoleCodes.Director };
        var admin = new Role { Name = "Admin", Code = RoleCodes.SystemAdmin };
        var custody = new Permission { Code = PermissionCodes.StockCustodyManage, Name = "Zimmet yönetme", GroupName = "Stok" };
        var view = new Permission { Code = PermissionCodes.StockView, Name = "Stok görüntüleme", GroupName = "Stok" };
        db.AddRange(director, admin, custody, view);
        db.RolePermissions.AddRange(new RolePermission { RoleId = director.Id, PermissionId = view.Id }, new RolePermission { RoleId = director.Id, PermissionId = custody.Id });
        db.AppSettings.Add(new() { Key = "Stock.CustodyManagementPermission20261022", Value = "true" });
        await db.SaveChangesAsync();
        await CustodyPermissionUpdate.SeedAsync(db);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.RoleId == director.Id && x.PermissionId == custody.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == director.Id && x.PermissionId == view.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == admin.Id && x.PermissionId == custody.Id));
        Assert.False(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.StockCustodyManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.StockView));
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(x => x.RoleId == admin.Id && x.PermissionId == custody.Id));
        await db.SaveChangesAsync();
        await CustodyPermissionUpdate.SeedAsync(db);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.RoleId == admin.Id && x.PermissionId == custody.Id));
    }

    [Theory]
    [InlineData(RoleCodes.SystemAdmin, PermissionCodes.StockManage, true)]
    [InlineData(RoleCodes.SystemAdmin, PermissionCodes.StockCustodyManage, true)]
    [InlineData(RoleCodes.SystemAdmin, PermissionCodes.StockView, false)]
    [InlineData(RoleCodes.Director, PermissionCodes.StockManage, false)]
    [InlineData(RoleCodes.Director, PermissionCodes.StockCustodyManage, false)]
    [InlineData(RoleCodes.AdministrativeOfficer, PermissionCodes.StockManage, false)]
    public async Task Custody_policy_requires_admin_role_even_with_old_write_permission(string role, string granted, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim(JwtTokenService.PermissionClaimType, granted)], "test"));
        var requirement = new PermissionRequirement(PermissionCodes.StockCustodyManage);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        Assert.Equal(expected, context.HasSucceeded);
    }
}
