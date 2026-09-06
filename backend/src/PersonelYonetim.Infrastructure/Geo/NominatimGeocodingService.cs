using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Application.Common.Interfaces;

namespace PersonelYonetim.Infrastructure.Geo;

public sealed class NominatimGeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NominatimGeocodingService> _logger;

    /// <summary>Şehitkamil viewbox: minLon,maxLat,maxLon,minLat</summary>
    private const string SehitkamilViewbox = "36.99,37.36,37.70,36.99";

    public NominatimGeocodingService(
        IHttpClientFactory httpClientFactory,
        ILogger<NominatimGeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GeocodeHit?> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 3) return null;

        var biased = ContainsLocalHint(q)
            ? q
            : $"{q}, Şehitkamil, Gaziantep, Türkiye";

        var url =
            "search?format=json&limit=1&countrycodes=tr&addressdetails=0" +
            $"&viewbox={SehitkamilViewbox}&bounded=0&q={Uri.EscapeDataString(biased)}";

        return await FetchFirstAsync(url, cancellationToken);
    }

    public async Task<GeocodeHit?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;

        var url =
            $"reverse?format=json&addressdetails=0&zoom=18&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        return await FetchFirstAsync(url, cancellationToken, isReverse: true);
    }

    private async Task<GeocodeHit?> FetchFirstAsync(
        string relativeUrl,
        CancellationToken cancellationToken,
        bool isReverse = false)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Nominatim");
            using var response = await client.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim {Status} for {Url}", (int)response.StatusCode, relativeUrl);
                return null;
            }

            if (isReverse)
            {
                var one = await response.Content.ReadFromJsonAsync<NominatimItem>(cancellationToken: cancellationToken);
                return Map(one);
            }

            var list = await response.Content.ReadFromJsonAsync<List<NominatimItem>>(cancellationToken: cancellationToken);
            return Map(list?.FirstOrDefault());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nominatim isteği başarısız");
            return null;
        }
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
