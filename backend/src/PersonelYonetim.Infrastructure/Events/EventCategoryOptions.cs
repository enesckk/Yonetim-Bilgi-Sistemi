using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;
namespace PersonelYonetim.Infrastructure.Events;
public sealed class GetEventCategoryOptionsHandler(AppDbContext db, ICurrentUserService user) : IRequestHandler<GetEventCategoryOptionsQuery, EventCategoryOptionsDto>
{
    public async Task<EventCategoryOptionsDto> Handle(GetEventCategoryOptionsQuery request, CancellationToken ct)
    {
        if (!user.HasPermission(PermissionCodes.EventsView) && !user.HasPermission(PermissionCodes.EventsManage)) throw new ForbiddenException("Etkinlik görüntüleme yetkisi gerekir.");
        var q = db.Events.AsNoTracking().Where(e => e.CategoryName != null && e.CategoryName != "");
        var scope = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (scope is not null) q = q.Where(e => e.FacilityId != null && scope.Contains(e.FacilityId.Value));
        var rows = await q.Select(e => new { e.Category, e.CategoryName }).Distinct().ToListAsync(ct);
        string[] Names(bool projects) => rows.Where(r => (r.Category == EventCategories.Project) == projects).Select(r => r.CategoryName!).Distinct(StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true)).OrderBy(n => n, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true)).ToArray();
        return new(Names(false), Names(true));
    }
}
