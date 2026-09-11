using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace PersonelYonetim.Infrastructure.Caching;

public static class AppCache
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public const string OrgTree = "lookup:org-tree";
    public const string OrgFormOptions = "lookup:org-form-options";
    public const string EmployeeFormOptions = "lookup:employee-form-options";
    public const string SettlementsAll = "lookup:settlements-all";
    public const string GeoSettlements = "geo:settlements-index";
    public const string MapEpoch = "map:epoch";

    public static readonly TimeSpan LookupTtl = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MapTtl = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan GeoTtl = TimeSpan.FromHours(6);

    public static void InvalidateOrgLookups(this IMemoryCache cache)
    {
        cache.Remove(OrgTree);
        cache.Remove(OrgFormOptions);
        cache.Remove(EmployeeFormOptions);
    }

    public static int MapVersion(this IMemoryCache cache) =>
        cache.TryGetValue(MapEpoch, out int version) ? version : 0;

    public static void InvalidateMapSummaries(this IMemoryCache cache)
    {
        cache.Set(MapEpoch, cache.MapVersion() + 1);
        cache.Remove(GeoSettlements);
    }

    public static async Task<T> GetOrCreateAsync<T>(
        this IMemoryCache cache,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
        where T : class
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            var value = await factory(cancellationToken);
            cache.Set(key, value, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });
            return value;
        }
        finally
        {
            gate.Release();
        }
    }
}
