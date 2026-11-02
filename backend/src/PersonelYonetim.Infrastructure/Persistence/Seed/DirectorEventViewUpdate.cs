using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class DirectorEventViewUpdate
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var links = await db.RolePermissions
            .Where(x => x.Role.Code == RoleCodes.Director && x.Permission.Code == PermissionCodes.EventsManage)
            .ToListAsync(ct);
        if (links.Count == 0) return;
        db.RolePermissions.RemoveRange(links);
        await db.SaveChangesAsync(ct);
    }
}
