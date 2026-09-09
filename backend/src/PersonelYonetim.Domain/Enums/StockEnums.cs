namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Kültür, sanat ve sosyal işler müdürlüğü stok grupları.
/// </summary>
public enum StockCategory : byte
{
    SoundAndLight = 1,
    StageAndShow = 2,
    Music = 3,
    Library = 4,
    Sports = 5,
    WorkshopAndScience = 6,
    ExhibitionAndArt = 7,
    Furniture = 8,
    ItEquipment = 9,
    Consumables = 10,
    Catering = 11,
    Safety = 12,
    OutdoorAndPark = 13,
    Other = 99
}

public enum StockUnit : byte
{
    Piece = 1,
    Set = 2,
    Pair = 3,
    Meter = 4,
    Kilogram = 5,
    Liter = 6,
    Box = 7,
    Pack = 8,
    Roll = 9
}

public enum StockMovementType : byte
{
    Inbound = 1,
    Outbound = 2,
    Transfer = 3,
    Adjustment = 4
}

public static class StockLabels
{
    public static string Category(StockCategory value) => value switch
    {
        StockCategory.SoundAndLight => "Ses ve ışık",
        StockCategory.StageAndShow => "Sahne ve gösteri",
        StockCategory.Music => "Müzik",
        StockCategory.Library => "Kütüphane",
        StockCategory.Sports => "Spor",
        StockCategory.WorkshopAndScience => "Atölye ve bilim",
        StockCategory.ExhibitionAndArt => "Sergi ve sanat",
        StockCategory.Furniture => "Mobilya ve demirbaş",
        StockCategory.ItEquipment => "Bilişim",
        StockCategory.Consumables => "Kırtasiye ve sarf",
        StockCategory.Catering => "İkram ve mutfak",
        StockCategory.Safety => "Güvenlik",
        StockCategory.OutdoorAndPark => "Dış alan ve park",
        _ => "Diğer"
    };

    public static string Unit(StockUnit value) => value switch
    {
        StockUnit.Piece => "Adet",
        StockUnit.Set => "Takım",
        StockUnit.Pair => "Çift",
        StockUnit.Meter => "Metre",
        StockUnit.Kilogram => "Kilogram",
        StockUnit.Liter => "Litre",
        StockUnit.Box => "Kutu",
        StockUnit.Pack => "Paket",
        StockUnit.Roll => "Rulo",
        _ => value.ToString()
    };

    public static string Movement(StockMovementType value) => value switch
    {
        StockMovementType.Inbound => "Giriş",
        StockMovementType.Outbound => "Çıkış",
        StockMovementType.Transfer => "Transfer",
        StockMovementType.Adjustment => "Sayım düzeltme",
        _ => value.ToString()
    };

    public static IReadOnlyList<(int Value, string Label)> CategoryOptions() =>
        Enum.GetValues<StockCategory>().Select(x => (Value: (int)x, Label: Category(x))).ToList();

    public static IReadOnlyList<(int Value, string Label)> UnitOptions() =>
        Enum.GetValues<StockUnit>().Select(x => (Value: (int)x, Label: Unit(x))).ToList();

    public static IReadOnlyList<(int Value, string Label)> MovementOptions() =>
        Enum.GetValues<StockMovementType>().Select(x => (Value: (int)x, Label: Movement(x))).ToList();
}
