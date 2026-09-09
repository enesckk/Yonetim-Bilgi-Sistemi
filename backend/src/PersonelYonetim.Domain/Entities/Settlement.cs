using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Şehitkamil yerleşimi (köy / kırsal mahalle / mahalle).
/// Geometri kaynak: frontend/public/geo/sehitkamil-mahalleler.geojson (ilçe ADM3).
/// </summary>
public class Settlement : AuditableEntity
{
    /// <summary>GeoJSON feature id (ör. TerritoryKit ADM3 kodu).</summary>
    public string OfficialCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>Mahalle | KirsalMahalle</summary>
    public string SettlementType { get; set; } = "Mahalle";

    public bool IsRural { get; set; }
    public double? CentroidLat { get; set; }
    public double? CentroidLng { get; set; }

    /// <summary>GeoJSON geometry JSON — nokta-poligon eşlemesi için.</summary>
    public string? GeometryJson { get; set; }

    public bool IsActive { get; set; } = true;

    public string? HeadmanName { get; set; }
    public string? HeadmanPhone { get; set; }

    public ICollection<SettlementPopulation> Populations { get; set; } = new List<SettlementPopulation>();
    public ICollection<EventSettlement> EventLinks { get; set; } = new List<EventSettlement>();
    public ICollection<SettlementSchool> Schools { get; set; } = new List<SettlementSchool>();
    public ICollection<SettlementArea> Areas { get; set; } = new List<SettlementArea>();
}
