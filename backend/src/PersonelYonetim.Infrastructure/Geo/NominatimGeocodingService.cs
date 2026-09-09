using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Settlements;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Geo;

public sealed class NominatimGeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NominatimGeocodingService> _logger;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;

    /// <summary>Şehitkamil viewbox: minLon,maxLat,maxLon,minLat</summary>
    private const string SehitkamilViewbox = "36.99,37.36,37.70,36.99";

    public NominatimGeocodingService(
        IHttpClientFactory httpClientFactory,
        ILogger<NominatimGeocodingService> logger,
        IMemoryCache cache,
        AppDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cache = cache;
        _db = db;
    }

    public async Task<GeocodeHit?> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var hits = await SuggestAsync(query, cancellationToken);
        return hits.Count > 0 ? hits[0] : null;
    }

    public async Task<IReadOnlyList<GeocodeHit>> SuggestAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 3) return [];

        var local = await MatchLocalAsync(q, cancellationToken);
        var strong = local.Where(h => SettlementPlaceMatch.IsStrong(q, h.DisplayName)).ToList();
        if (strong.Count > 0)
            return strong.Take(6).ToArray();

        var biased = ContainsLocalHint(q)
            ? q
            : $"{q}, Şehitkamil, Gaziantep, Türkiye";

        var url =
            "search?format=json&limit=5&countrycodes=tr&addressdetails=0" +
            $"&viewbox={SehitkamilViewbox}&bounded=0&q={Uri.EscapeDataString(biased)}";

        var remote = await FetchListAsync(url, cancellationToken);
        return Merge(local, remote);
    }

    public async Task<GeocodeHit?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;

        var cacheKey =
            $"geo:rev:{latitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}:{longitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}";
        if (_cache.TryGetValue(cacheKey, out GeocodeHit? cached))
            return cached;

        var url =
            $"reverse?format=json&addressdetails=0&zoom=18&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        try
        {
            var client = _httpClientFactory.CreateClient("Nominatim");
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim {Status} for reverse", (int)response.StatusCode);
                return null;
            }

            var one = await response.Content.ReadFromJsonAsync<NominatimItem>(cancellationToken: cancellationToken);
            var hit = Map(one);
            if (hit is not null)
                _cache.Set(cacheKey, hit, AppCache.GeoTtl);
            return hit;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nominatim reverse isteği başarısız");
            return null;
        }
    }

    private async Task<IReadOnlyList<GeocodeHit>> MatchLocalAsync(string query, CancellationToken cancellationToken)
    {
        var places = await _cache.GetOrCreateAsync(
            AppCache.GeoSettlements,
            TimeSpan.FromHours(6),
            LoadLocalPlacesAsync,
            cancellationToken);

        var hits = new List<GeocodeHit>();
        foreach (var place in places)
        {
            if (!SettlementPlaceMatch.IsLoose(query, place.Name))
                continue;
            hits.Add(new GeocodeHit(place.Lat, place.Lng, place.Name));
            if (hits.Count >= 6)
                break;
        }

        return hits;
    }

    private async Task<LocalPlace[]> LoadLocalPlacesAsync(CancellationToken cancellationToken)
    {
        return await _db.Settlements.AsNoTracking()
            .Where(s => s.IsActive && s.CentroidLat != null && s.CentroidLng != null)
            .OrderBy(s => s.Name)
            .Select(s => new LocalPlace(
                s.DisplayName != null && s.DisplayName != "" ? s.DisplayName : s.Name,
                s.CentroidLat!.Value,
                s.CentroidLng!.Value))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<GeocodeHit>> FetchListAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var cacheKey = "geo:search:" + relativeUrl;
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<GeocodeHit>? cached) && cached is not null)
            return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("Nominatim");
            using var response = await client.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim {Status} for {Url}", (int)response.StatusCode, relativeUrl);
                return [];
            }

            var list = await response.Content.ReadFromJsonAsync<List<NominatimItem>>(cancellationToken: cancellationToken);
            if (list is null || list.Count == 0) return [];

            var hits = new List<GeocodeHit>(list.Count);
            foreach (var item in list)
            {
                var mapped = Map(item);
                if (mapped is null) continue;
                if (hits.Any(h => Math.Abs(h.Latitude - mapped.Latitude) < 0.00015 && Math.Abs(h.Longitude - mapped.Longitude) < 0.00015))
                    continue;
                hits.Add(mapped);
            }

            _cache.Set(cacheKey, (IReadOnlyList<GeocodeHit>)hits, AppCache.GeoTtl);
            return hits;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nominatim isteği başarısız");
            return [];
        }
    }

    private static IReadOnlyList<GeocodeHit> Merge(IReadOnlyList<GeocodeHit> local, IReadOnlyList<GeocodeHit> remote)
    {
        var merged = new List<GeocodeHit>(local);
        foreach (var hit in remote)
        {
            if (merged.Any(h =>
                    Math.Abs(h.Latitude - hit.Latitude) < 0.00015 &&
                    Math.Abs(h.Longitude - hit.Longitude) < 0.00015))
                continue;
            merged.Add(hit);
            if (merged.Count >= 6)
                break;
        }
        return merged;
    }

    private static GeocodeHit? Map(NominatimItem? item)
    {
        if (item is null) return null;
        if (!double.TryParse(item.Lat, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!double.TryParse(item.Lon, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lon))
            return null;
        var name = string.IsNullOrWhiteSpace(item.DisplayName) ? $"{lat:F5}, {lon:F5}" : item.DisplayName!;
        return new GeocodeHit(lat, lon, name);
    }

    private static bool ContainsLocalHint(string q) =>
        q.Contains("gaziantep", StringComparison.OrdinalIgnoreCase) ||
        q.Contains("şehitkamil", StringComparison.OrdinalIgnoreCase) ||
        q.Contains("sehitkamil", StringComparison.OrdinalIgnoreCase);

    private sealed record LocalPlace(string Name, double Lat, double Lng);

    private sealed class NominatimItem
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
