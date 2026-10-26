using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class OfficerAccountTests
{
    [Fact]
    public async Task Provisioning_creates_one_officer_with_expected_access_without_resetting_password()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var role = new Role { Code = RoleCodes.Officer, Name = "Memur" };
        db.Roles.Add(role);
        foreach (var definition in PermissionCatalog.All)
            db.Permissions.Add(new Permission { Code = definition.Code, Name = definition.Code, GroupName = "Test" });
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:OfficerPassword"] = "TestOfficer!12345"
        }).Build();
        await OfficerAccountSeeder.SeedAsync(db, configuration);
        var user = await db.Users.SingleAsync();
        var originalHash = user.PasswordHash;
        Assert.Equal("memur", user.UserName);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(user, originalHash, "TestOfficer!12345"));
        Assert.Equal(role.Id, (await db.UserRoles.SingleAsync()).RoleId);
        var codes = await db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.Permission.Code).ToListAsync();
        Assert.Contains(PermissionCodes.StockCustodyManage, codes);
        Assert.Contains(PermissionCodes.StockManage, codes);
        Assert.Contains(PermissionCodes.EmployeesViewAllUnits, codes);
        Assert.Contains(PermissionCodes.EmployeesViewNationalId, codes);
        Assert.Contains(PermissionCodes.OrganizationView, codes);
        Assert.Contains(PermissionCodes.MessagesUse, codes);
        Assert.Contains(PermissionCodes.NotificationsView, codes);
        Assert.DoesNotContain(PermissionCodes.EventsView, codes);
        Assert.DoesNotContain(PermissionCodes.EventsManage, codes);
        Assert.DoesNotContain(PermissionCodes.UsersManage, codes);
        Assert.DoesNotContain(PermissionCodes.EmployeesUpdate, codes);
        configuration["Seed:OfficerPassword"] = "ChangedPassword!123";
        await OfficerAccountSeeder.SeedAsync(db, configuration);
        Assert.Single(await db.Users.ToListAsync());
        Assert.Equal(originalHash, (await db.Users.SingleAsync()).PasswordHash);
    }
}
