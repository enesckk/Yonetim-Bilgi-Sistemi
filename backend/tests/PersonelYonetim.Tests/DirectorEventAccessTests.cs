using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Auth;
using PersonelYonetim.Infrastructure.Identity;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class DirectorEventAccessTests
{
    [Theory]
    [InlineData(RoleCodes.Director, PermissionCodes.EventsManage, false)]
    [InlineData(RoleCodes.Director, PermissionCodes.EventsView, true)]
    [InlineData(RoleCodes.Director, PermissionCodes.EmployeesUpdate, true)]
    [InlineData(RoleCodes.SystemAdmin, PermissionCodes.EventsManage, true)]
    [InlineData(RoleCodes.AdministrativeOfficer, PermissionCodes.EventsManage, true)]
    [InlineData(RoleCodes.DeputyDirector, PermissionCodes.EventsManage, true)]
    public async Task Legacy_claims_follow_director_readonly_policy(string role, string permission, bool expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim(JwtTokenService.PermissionClaimType, permission)], "test"));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        Assert.Equal(expected, context.HasSucceeded);
        var current = new CurrentUserService(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        Assert.Equal(expected, current.HasPermission(permission));
        Assert.Equal(expected, current.Permissions.Contains(permission));
    }

    [Fact]
    public async Task Upgrade_removes_only_director_event_writes_and_is_repeatable()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        await db.Database.EnsureCreatedAsync();
        var director = new Role { Code = RoleCodes.Director, Name = "Müdür" };
        var admin = new Role { Code = RoleCodes.SystemAdmin, Name = "Admin" };
        var manage = new Permission { Code = PermissionCodes.EventsManage, Name = "Yönetme", GroupName = "Etkinlik" };
        var view = new Permission { Code = PermissionCodes.EventsView, Name = "Görüntüleme", GroupName = "Etkinlik" };
        db.AddRange(director, admin, manage, view);
        db.RolePermissions.AddRange(new RolePermission { Role = director, Permission = manage }, new RolePermission { Role = director, Permission = view }, new RolePermission { Role = admin, Permission = manage });
        await db.SaveChangesAsync();
        await DirectorEventViewUpdate.SeedAsync(db);
        await DirectorEventViewUpdate.SeedAsync(db);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.RoleId == director.Id && x.PermissionId == manage.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == director.Id && x.PermissionId == view.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == admin.Id && x.PermissionId == manage.Id));
        Assert.False(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.EventsManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(RoleCodes.Director, PermissionCodes.EventsView));
    }
}
