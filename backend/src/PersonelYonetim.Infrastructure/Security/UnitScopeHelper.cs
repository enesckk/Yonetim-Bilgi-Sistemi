using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Security;

/// <summary>
/// ViewAllUnits yoksa kullanıcı yalnızca bağlı olduğu birim ve altındaki kadroyu görür.
/// </summary>
internal static class UnitScopeHelper
{
    /// <summary>null = kısıt yok (tüm birimler). Boş küme = bağlı birim yok.</summary>
    public static async Task<HashSet<Guid>?> AllowedUnitIdsAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.HasPermission(PermissionCodes.EmployeesViewAllUnits))
            return null;

        if (currentUser.UserId is null)
            return [];

        var unitId = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUser.UserId)
            .Select(x => x.Employee != null ? x.Employee.UnitId : null)
            .FirstOrDefaultAsync(cancellationToken);

        if (unitId is null)
            return [];

        return await DescendantsOfAsync(db, unitId.Value, cancellationToken);
    }

    /// <summary>İzin verilen birimler + şemada omurga için ataları.</summary>
    public static async Task<HashSet<Guid>?> VisibleUnitIdsAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var allowed = await AllowedUnitIdsAsync(db, currentUser, cancellationToken);
        if (allowed is null)
            return null;
        if (allowed.Count == 0)
            return allowed;

        var rows = await db.OrganizationUnits
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);
        var parentById = rows.ToDictionary(x => x.Id, x => x.ParentId);

        var keep = new HashSet<Guid>(allowed);
        foreach (var id in allowed)
        {
            var current = parentById.GetValueOrDefault(id);
            while (current is Guid parentId && keep.Add(parentId))
                current = parentById.GetValueOrDefault(parentId);
        }

        return keep;
    }

    public static async Task<IQueryable<Employee>?> ApplyAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        IQueryable<Employee> employees,
        CancellationToken cancellationToken)
    {
        var allowed = await AllowedUnitIdsAsync(db, currentUser, cancellationToken);
        if (allowed is null)
            return employees;
        if (allowed.Count == 0)
            return null;

        return employees.Where(x =>
            (x.UnitId != null && allowed.Contains(x.UnitId.Value))
            || (x.FacilityId != null && allowed.Contains(x.FacilityId.Value)));
    }

    private static async Task<HashSet<Guid>> DescendantsOfAsync(
        AppDbContext db,
        Guid rootUnitId,
        CancellationToken cancellationToken)
    {
        var rows = await db.OrganizationUnits
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = rows
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<Guid> { rootUnitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var childId in children)
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }
}
