using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Audit;

public sealed class SensitiveAccessLogger : ISensitiveAccessLogger
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _http;

    public SensitiveAccessLogger(
        AppDbContext db,
        ICurrentUserService currentUser,
        IHttpContextAccessor http)
    {
        _db = db;
        _currentUser = currentUser;
        _http = http;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var ua = _http.HttpContext?.Request.Headers.UserAgent.ToString();
        if (ua is { Length: > 500 })
            ua = ua[..500];

        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = _currentUser.UserId?.ToString(),
            UserName = _currentUser.UserName ?? "anonymous",
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ua,
            NewValuesJson = details is null ? null : JsonSerializer.Serialize(details)
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
