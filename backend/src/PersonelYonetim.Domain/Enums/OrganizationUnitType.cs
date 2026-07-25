namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Organizasyon ağacındaki düğüm türü.
/// Belediye → Başkan Yardımcılığı → Müdürlük → Ana Birim → Alt Birim → Tesis
/// </summary>
public enum OrganizationUnitType : byte
{
    Municipality = 1,
    DeputyPresidency = 2,
    Directorate = 3,
    MainUnit = 4,
    SubUnit = 5,
    Facility = 6
}
