using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence;

/// <summary>
/// SaveChanges öncesi otomatik audit — her CommandService'e tek tek log yazmaya gerek yok.
/// Öğrenme: interceptor = çapraz kesen (cross-cutting) kalıcılık kancası.
/// </summary>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> SkippedEntityNames = new(StringComparer.Ordinal)
    {
        nameof(AuditLog),
        nameof(RefreshToken)
    };

    private static readonly HashSet<string> MaskedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "NationalIdEncrypted",
        "PasswordHash",
        "TokenHash"
    };

    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditingSaveChangesInterceptor(
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEntries(DbContext? context)
    {
        if (context is null)
            return;

        var http = _httpContextAccessor.HttpContext;
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        var ua = http?.Request.Headers.UserAgent.ToString();
        if (ua is { Length: > 500 })
            ua = ua[..500];

        var userId = _currentUser.UserId?.ToString();
        var userName = _currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        // ToList: ChangeTracker, Add sırasında değişebilir
        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !SkippedEntityNames.Contains(e.Metadata.ClrType.Name))
            .ToList();

        foreach (var entry in entries)
        {
            var action = ResolveAction(entry);
            var (oldJson, newJson) = CaptureValues(entry);

            context.Set<AuditLog>().Add(new AuditLog
            {
                OccurredAtUtc = now,
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = GetPrimaryKey(entry),
                OldValuesJson = oldJson,
                NewValuesJson = newJson,
                IpAddress = ip,
                UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua
            });
        }
    }

    private static string ResolveAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
            return "Create";
        if (entry.State == EntityState.Deleted)
            return "Delete";

        // Soft-delete: IsDeleted false → true
        if (entry.Entity is AuditableEntity
            && entry.Property(nameof(AuditableEntity.IsDeleted)).IsModified
            && entry.Property(nameof(AuditableEntity.IsDeleted)).CurrentValue is true)
        {
            return "SoftDelete";
        }

        return "Update";
    }

    private static (string? OldJson, string? NewJson) CaptureValues(EntityEntry entry)
    {
        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (prop.Metadata.IsPrimaryKey())
                continue;

            // Navigation shadow / concurrency token gürültüsü azalt
            if (name is "xmin" or "RowVersion")
                continue;

            var current = Mask(name, prop.CurrentValue);
            var original = Mask(name, prop.OriginalValue);

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues ??= new Dictionary<string, object?>();
                    newValues[name] = current;
                    break;
                case EntityState.Deleted:
                    oldValues ??= new Dictionary<string, object?>();
                    oldValues[name] = original;
                    break;
                case EntityState.Modified:
                    if (!prop.IsModified)
                        continue;
                    oldValues ??= new Dictionary<string, object?>();
                    newValues ??= new Dictionary<string, object?>();
                    oldValues[name] = original;
                    newValues[name] = current;
                    break;
            }
        }

        return (Serialize(oldValues), Serialize(newValues));
    }

    private static object? Mask(string propertyName, object? value)
    {
        if (value is null)
            return null;
        if (MaskedProperties.Contains(propertyName))
            return "***";
        if (value is DateTime dt)
            return dt.ToUniversalTime().ToString("O");
        if (value is DateOnly d)
            return d.ToString("yyyy-MM-dd");
        return value;
    }

    private static string? Serialize(Dictionary<string, object?>? values)
    {
        if (values is null || values.Count == 0)
            return null;
        return JsonSerializer.Serialize(values);
    }

    private static string? GetPrimaryKey(EntityEntry entry)
    {
        var key = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return key?.CurrentValue?.ToString() ?? key?.OriginalValue?.ToString();
    }
}
