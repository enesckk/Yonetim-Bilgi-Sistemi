using System.Text.Json;

namespace PersonelYonetim.Infrastructure.Settlements;

/// <summary>Basit GeoJSON nokta-poligon — NetTopologySuite yok.</summary>
public static class SettlementGeoHelper
{
    public static bool TryCentroid(string? geometryJson, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        if (string.IsNullOrWhiteSpace(geometryJson))
            return false;

        using var doc = JsonDocument.Parse(geometryJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("type", out var typeEl))
            return false;

        var type = typeEl.GetString();
        var sumLat = 0d;
        var sumLng = 0d;
        var n = 0;

        if (type == "Polygon")
        {
            AccumulateRing(root.GetProperty("coordinates")[0], ref sumLat, ref sumLng, ref n);
        }
        else if (type == "MultiPolygon")
        {
            foreach (var poly in root.GetProperty("coordinates").EnumerateArray())
                AccumulateRing(poly[0], ref sumLat, ref sumLng, ref n);
        }

        if (n == 0)
            return false;

        lat = sumLat / n;
        lng = sumLng / n;
        return true;
    }

    public static bool Contains(string? geometryJson, double lat, double lng)
    {
        if (string.IsNullOrWhiteSpace(geometryJson))
            return false;

        using var doc = JsonDocument.Parse(geometryJson);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

        if (type == "Polygon")
            return InPolygon(root.GetProperty("coordinates"), lat, lng);

        if (type == "MultiPolygon")
        {
            foreach (var poly in root.GetProperty("coordinates").EnumerateArray())
            {
                if (InPolygon(poly, lat, lng))
                    return true;
            }
        }

        return false;
    }

    private static void AccumulateRing(JsonElement ring, ref double sumLat, ref double sumLng, ref int n)
    {
        foreach (var pt in ring.EnumerateArray())
        {
            if (pt.GetArrayLength() < 2) continue;
            sumLng += pt[0].GetDouble();
            sumLat += pt[1].GetDouble();
            n++;
        }
    }

    private static bool InPolygon(JsonElement polygonCoords, double lat, double lng)
    {
        if (polygonCoords.GetArrayLength() == 0)
            return false;

        var outer = polygonCoords[0];
        if (!InRing(outer, lat, lng))
            return false;

        for (var i = 1; i < polygonCoords.GetArrayLength(); i++)
        {
            if (InRing(polygonCoords[i], lat, lng))
                return false;
        }

        return true;
    }

    private static bool InRing(JsonElement ring, double lat, double lng)
    {
        var inside = false;
        var pts = ring.EnumerateArray().ToList();
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            if (pts[i].GetArrayLength() < 2 || pts[j].GetArrayLength() < 2)
                continue;
            var xi = pts[i][0].GetDouble();
            var yi = pts[i][1].GetDouble();
            var xj = pts[j][0].GetDouble();
            var yj = pts[j][1].GetDouble();
            var intersect = yi > lat != yj > lat
                && lng < (xj - xi) * (lat - yi) / (yj - yi + double.Epsilon) + xi;
            if (intersect) inside = !inside;
        }

        return inside;
    }
}
