namespace PersonelYonetim.Application.Features.Geo;

public sealed class GeocodeResultDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class GeocodeLookupDto
{
    public bool Found { get; init; }
    public GeocodeResultDto? Result { get; init; }
}

public sealed class GeocodeSuggestDto
{
    public IReadOnlyList<GeocodeResultDto> Results { get; init; } = [];
}
