using MediatR;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Application.Features.Audit;

public sealed class GetAuditLogsQuery : IRequest<PagedResult<AuditLogListItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public string? UserName { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class AuditLogListItemDto
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? OldValuesJson { get; init; }
    public string? NewValuesJson { get; init; }
    public string? IpAddress { get; init; }
}
