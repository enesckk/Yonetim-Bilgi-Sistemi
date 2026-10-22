using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Stock;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class CustodyPermissionUpdate
{
    public const string Marker = "Stock.CustodyManagementPermission20261022";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await StockTransaction.RunAsync(db, async () =>
        {
            if (await db.AppSettings.IgnoreQueryFilters().AnyAsync(x => x.Key == Marker, ct)) return false;
            var permission = await db.Permissions.SingleAsync(x => x.Code == PermissionCodes.StockCustodyManage, ct);
            var roles = await db.Roles.Where(x => x.Code == RoleCodes.Director || x.Code == RoleCodes.SystemAdmin).ToListAsync(ct);
            foreach (var role in roles)
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, CreatedBy = "custody-permission-update" });
            db.AppSettings.Add(new AppSetting
            {
                Key = Marker, Value = "true", ValueType = AppSettingValueType.Boolean, GroupName = "Stok",
                DisplayName = "Zimmet yönetim yetkisi tanımlandı", IsReadOnly = true,
                Description = "Müdür ve sistem yöneticisi için zimmet atama, devir ve iade yetkisi bir defa tanımlandı."
            });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }
}
