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
    public async Task Director_gets_custody_management_without_general_stock_write_and_revocations_are_preserved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var role = new Role { Name = "Müdür", Code = RoleCodes.Director };
        var custody = new Permission { Code = PermissionCodes.StockCustodyManage, Name = "Zimmet yönetme", GroupName = "Stok" };
        var view = new Permission { Code = PermissionCodes.StockView, Name = "Stok görüntüleme", GroupName = "Stok" };
        db.AddRange(role, custody, view);
        db.RolePermissions.Add(new() { RoleId = role.Id, PermissionId = view.Id });
        await db.SaveChangesAsync();
        await CustodyPermissionUpdate.SeedAsync(db);
        Assert.Equal(2, await db.RolePermissions.CountAsync());
        Assert.True(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.StockCustodyManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.StockManage));
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(x => x.PermissionId == custody.Id));
        await db.SaveChangesAsync();
        await CustodyPermissionUpdate.SeedAsync(db);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.PermissionId == custody.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.PermissionId == view.Id));
    }

    [Theory]
    [InlineData(PermissionCodes.StockManage, PermissionCodes.StockCustodyManage, true)]
    [InlineData(PermissionCodes.StockCustodyManage, PermissionCodes.StockManage, false)]
    [InlineData(PermissionCodes.StockView, PermissionCodes.StockCustodyManage, false)]
    [InlineData(PermissionCodes.StockCustodyManage, PermissionCodes.StockCustodyManage, true)]
    public async Task General_stock_permission_includes_custody_but_custody_does_not_grant_general_stock(string granted, string requested, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtTokenService.PermissionClaimType, granted)], "test"));
        var requirement = new PermissionRequirement(requested);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        Assert.Equal(expected, context.HasSucceeded);
    }
}
