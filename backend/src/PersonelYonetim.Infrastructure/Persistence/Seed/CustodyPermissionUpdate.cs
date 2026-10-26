using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class CustodyPermissionUpdate
{
    public const string Marker = "Stock.AdminOnlyCustodyPermission20261022";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await StockTransaction.RunAsync(db, async () =>
        {
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == Marker, ct)) return false;
            var permission = await db.Permissions.SingleAsync(x => x.Code == PermissionCodes.StockCustodyManage, ct);
            var nonAdminLinks = await db.RolePermissions.Include(x => x.Role)
                .Where(x => x.PermissionId == permission.Id && x.Role.Code != RoleCodes.SystemAdmin && x.Role.Code != RoleCodes.Officer).ToListAsync(ct);
            db.RolePermissions.RemoveRange(nonAdminLinks);
            var roles = await db.Roles.Where(x => x.Code == RoleCodes.SystemAdmin).ToListAsync(ct);
            foreach (var role in roles)
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, CreatedBy = "custody-permission-update" });
            db.AppSettings.Add(new AppSetting
            {
                Key = Marker, Value = "true", ValueType = AppSettingValueType.Boolean, GroupName = "Stok",
                DisplayName = "Zimmet yönetim yetkisi tanımlandı", IsReadOnly = true,
                Description = "Zimmet yönetimi yalnızca sistem yöneticisine ayrıldı; müdür görüntüleme yetkisi korundu."
            });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }
}
