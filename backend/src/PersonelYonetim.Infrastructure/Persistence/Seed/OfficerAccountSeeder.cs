using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class OfficerAccountSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration, CancellationToken ct = default)
    {
        var role = await db.Roles.SingleAsync(x => x.Code == RoleCodes.Officer, ct);
        const string marker = "Security.OfficerPermissions20261026";
        if (!await db.AppSettings.AnyAsync(x => x.Key == marker, ct))
        {
            var codes = RolePermissionMatrix.GetPermissionsForRole(RoleCodes.Officer);
            var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
            var existing = await db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync(ct);
            foreach (var permission in permissions.Where(x => !existing.Contains(x.Id)))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.AppSettings.Add(new AppSetting { Key = marker, Value = "true", GroupName = "Yetkiler", DisplayName = "Memur yetkileri tanımlandı", IsReadOnly = true });
            await db.SaveChangesAsync(ct);
        }

        // Explicitly requested initial account; a marker prevents recreation after deletion.
        // Only a salted Identity hash is stored in source, never the initial password.
        const string accountMarker = "Security.OfficerAccount20261026";
        if (await db.AppSettings.AnyAsync(x => x.Key == accountMarker, ct)) return;
        var password = configuration["Seed:OfficerPassword"];
        var name = configuration["Seed:OfficerUserName"] ?? "memur";
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.UserName == name, ct)) return;
        if (password is not null && password.Length < 12)
            throw new InvalidOperationException("Memur başlangıç şifresi en az 12 karakter olmalıdır.");
        var user = new AppUser { UserName = name, DisplayName = "Memur", IsActive = true };
        user.PasswordHash = password is null
            ? "AQAAAAIAAYagAAAAEGFTyv8SGcJsgLXvDfsZ8c67bvTsBtxs3aG3HVJuM231Nfle+CvBUTJKkwWipSMsvw=="
            : new PasswordHasher<AppUser>().HashPassword(user, password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.AppSettings.Add(new AppSetting { Key = accountMarker, Value = "true", GroupName = "Yetkiler", DisplayName = "İlk memur hesabı oluşturuldu", IsReadOnly = true });
        await db.SaveChangesAsync(ct);
    }
}
