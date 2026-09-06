namespace PersonelYonetim.Application.Common.Interfaces;

public interface IGeocodingService
{
    Task<GeocodeHit?> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<GeocodeHit?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public sealed record GeocodeHit(double Latitude, double Longitude, string DisplayName);
