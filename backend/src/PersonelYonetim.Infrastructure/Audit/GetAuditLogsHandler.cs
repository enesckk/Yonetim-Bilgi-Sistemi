using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Audit;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Audit;

public sealed class GetAuditLogsHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogListItemDto>>
{
    private readonly AppDbContext _db;

    public GetAuditLogsHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<AuditLogListItemDto>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityName))
            query = query.Where(x => x.EntityName == request.EntityName.Trim());

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(x => x.Action == request.Action.Trim());

        if (!string.IsNullOrWhiteSpace(request.UserName))
            query = query.Where(x => x.UserName != null && x.UserName.Contains(request.UserName.Trim()));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.EntityId != null && x.EntityId.Contains(s))
                || (x.UserName != null && x.UserName.Contains(s))
                || x.EntityName.Contains(s)
                || x.Action.Contains(s));
        }

        if (request.From is { } fromDate)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.OccurredAtUtc >= fromUtc);
        }

        if (request.To is { } toDate)
        {
            var toUtcExclusive = DateTime.SpecifyKind(
                toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
            query = query.Where(x => x.OccurredAtUtc < toUtcExclusive);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogListItemDto
            {
                Id = x.Id,
                OccurredAtUtc = x.OccurredAtUtc,
                UserName = x.UserName,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                OldValuesJson = x.OldValuesJson,
                NewValuesJson = x.NewValuesJson,
                IpAddress = x.IpAddress
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
