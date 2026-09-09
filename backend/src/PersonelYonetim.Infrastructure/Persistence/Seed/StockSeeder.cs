using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

internal static class StockSeeder
{
    public static async Task SeedAsync(AppDbContext db, IHostEnvironment environment, ILogger logger, CancellationToken ct)
    {
        var catalog = Catalog();
        var existing = await db.StockItems.IgnoreQueryFilters()
            .Where(x => x.Code != null)
            .Select(x => x.Code!)
            .ToListAsync(ct);
        var codes = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var row in catalog)
        {
            if (codes.Contains(row.Code))
                continue;
            db.StockItems.Add(new StockItem
            {
                Name = row.Name,
                Code = row.Code,
                Category = row.Category,
                Unit = row.Unit,
                Brand = row.Brand,
                MinQuantity = row.Min,
                Description = row.Description,
                IsActive = true
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seed: {Count} stok malzemesi eklendi.", added);
        }

        if (!environment.IsDevelopment())
            return;

        if (await db.StockBalances.AnyAsync(ct))
            return;

        var items = await db.StockItems.Where(x => x.Code != null).ToDictionaryAsync(x => x.Code!, ct);
        var places = await db.OrganizationUnits
            .Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, ct);

        bool Place(string code, out Guid id) => places.TryGetValue(code, out id);
        bool Item(string code, out StockItem item)
        {
            if (items.TryGetValue(code, out item!))
                return true;
            item = null!;
            return false;
        }

        var samples = new List<(string Place, string Item, decimal Qty)>();
        void Add(string place, string item, decimal qty) => samples.Add((place, item, qty));

        Add("FAC_SANAT", "SES-MIK-EL", 12);
        Add("FAC_SANAT", "SES-MIK-YAKA", 8);
        Add("FAC_SANAT", "SES-MIX-16", 2);
        Add("FAC_SANAT", "SES-LED-PAR", 24);
        Add("FAC_SANAT", "SES-MOVING", 6);
        Add("FAC_SANAT", "SAH-PODYUM", 18);
        Add("FAC_SANAT", "SAH-FON", 4);
        Add("FAC_SANAT", "MOB-SANDALYE", 220);
        Add("FAC_SANAT", "MOB-KURSÜ", 2);
        Add("FAC_SANAT", "BIL-PROJ", 3);

        Add("FAC_KKM", "SES-MIK-EL", 16);
        Add("FAC_KKM", "SES-HOP-AKTIF", 8);
        Add("FAC_KKM", "SES-MIX-16", 3);
        Add("FAC_KKM", "SAH-PODYUM", 30);
        Add("FAC_KKM", "MOB-SANDALYE", 480);
        Add("FAC_KKM", "MOB-KATLANIR", 200);
        Add("FAC_KKM", "BIL-PROJ", 6);
        Add("FAC_KKM", "BIL-LAPTOP", 4);
        Add("FAC_KKM", "IKR-CAY", 3);
        Add("FAC_KKM", "GUV-YANGIN", 12);

        Add("FAC_KUT", "KUT-KITAP", 4200);
        Add("FAC_KUT", "KUT-COCUK", 860);
        Add("FAC_KUT", "KUT-RAF", 48);
        Add("FAC_KUT", "KUT-MASA", 24);
        Add("FAC_KUT", "MOB-SANDALYE", 72);
        Add("FAC_KUT", "BIL-EKRAN", 8);

        Add("FAC_SPOR", "SPR-MASA-TENIS", 6);
        Add("FAC_SPOR", "SPR-RAKET", 24);
        Add("FAC_SPOR", "SPR-FUTBOL", 18);
        Add("FAC_SPOR", "SPR-BASKET", 12);
        Add("FAC_SPOR", "SPR-MINDER", 20);

        Add("FAC_BILIM", "ATL-3D", 3);
        Add("FAC_BILIM", "ATL-ROBOT", 12);
        Add("FAC_BILIM", "ATL-MIKRO", 10);
        Add("FAC_BILIM", "ATL-BOYA", 40);
        Add("FAC_BILIM", "BIL-LAPTOP", 10);

        Add("AGROPARK", "DIS-HORTUM", 14);
        Add("AGROPARK", "DIS-BANK", 32);
        Add("AGROPARK", "SPR-FUTBOL", 8);
        Add("AGROPARK", "GUV-BARIYER", 20);

        Add("FAC_NIKAH", "MOB-SANDALYE", 160);
        Add("FAC_NIKAH", "SES-MIK-EL", 4);
        Add("FAC_NIKAH", "BIL-PROJ", 2);
        Add("FAC_NIKAH", "IKR-TEPSI", 12);

        var created = 0;
        foreach (var sample in samples)
        {
            if (!Place(sample.Place, out var locId) || !Item(sample.Item, out var stockItem))
                continue;
            db.StockBalances.Add(new StockBalance
            {
                StockItemId = stockItem.Id,
                LocationId = locId,
                Quantity = sample.Qty
            });
            db.StockMovements.Add(new StockMovement
            {
                StockItemId = stockItem.Id,
                MovementType = StockMovementType.Inbound,
                ToLocationId = locId,
                Quantity = sample.Qty,
                OccurredOn = DateOnly.FromDateTime(DateTime.Today),
                Reason = "Başlangıç sayımı"
            });
            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seed: {Count} örnek stok bakiyesi eklendi.", created);
        }
    }

    private static IReadOnlyList<(string Code, string Name, StockCategory Category, StockUnit Unit, decimal Min, string? Brand, string? Description)> Catalog() =>
    [
        ("SES-MIK-EL", "Kablosuz el mikrofonu", StockCategory.SoundAndLight, StockUnit.Piece, 4, null, "Sahne ve salon konuşmaları"),
        ("SES-MIK-YAKA", "Yaka mikrofonu", StockCategory.SoundAndLight, StockUnit.Piece, 4, null, null),
        ("SES-HOP-AKTIF", "Aktif hoparlör", StockCategory.SoundAndLight, StockUnit.Piece, 2, null, null),
        ("SES-MIX-16", "16 kanal mixer", StockCategory.SoundAndLight, StockUnit.Piece, 1, null, null),
        ("SES-ISIK-MASA", "Işık masası", StockCategory.SoundAndLight, StockUnit.Piece, 1, null, null),
        ("SES-LED-PAR", "LED PAR ışık", StockCategory.SoundAndLight, StockUnit.Piece, 8, null, "Sahne yıkama ışığı"),
        ("SES-MOVING", "Moving head", StockCategory.SoundAndLight, StockUnit.Piece, 2, null, null),
        ("SES-SIS", "Sis makinesi", StockCategory.SoundAndLight, StockUnit.Piece, 1, null, null),
        ("SES-XLR", "XLR kablo", StockCategory.SoundAndLight, StockUnit.Meter, 20, null, null),
        ("SES-DI-BOX", "DI box", StockCategory.SoundAndLight, StockUnit.Piece, 2, null, null),

        ("SAH-PODYUM", "Podyum / sahne platformu", StockCategory.StageAndShow, StockUnit.Piece, 6, null, "Modüler sahne parçası"),
        ("SAH-FON", "Fon perde", StockCategory.StageAndShow, StockUnit.Piece, 2, null, null),
        ("SAH-PARAVAN", "Dekor paravan", StockCategory.StageAndShow, StockUnit.Piece, 4, null, null),
        ("SAH-KOSTUM", "Kostüm askılığı", StockCategory.StageAndShow, StockUnit.Piece, 2, null, null),
        ("SAH-MERDIVEN", "Sahne merdiveni", StockCategory.StageAndShow, StockUnit.Piece, 2, null, null),

        ("MUZ-GITAR", "Klasik gitar", StockCategory.Music, StockUnit.Piece, 4, null, "Atölye ve kurs"),
        ("MUZ-BAGLAMA", "Bağlama", StockCategory.Music, StockUnit.Piece, 4, null, null),
        ("MUZ-ORG", "Org / dijital piyano", StockCategory.Music, StockUnit.Piece, 1, null, null),
        ("MUZ-NOTA", "Nota sehpası", StockCategory.Music, StockUnit.Piece, 6, null, null),
        ("MUZ-DAVUL", "Davul seti", StockCategory.Music, StockUnit.Set, 1, null, null),

        ("KUT-KITAP", "Kitap", StockCategory.Library, StockUnit.Piece, 500, null, "Genel koleksiyon"),
        ("KUT-COCUK", "Çocuk kitabı", StockCategory.Library, StockUnit.Piece, 100, null, null),
        ("KUT-RAF", "Kütüphane raf ünitesi", StockCategory.Library, StockUnit.Piece, 8, null, null),
        ("KUT-MASA", "Okuma masası", StockCategory.Library, StockUnit.Piece, 6, null, null),

        ("SPR-MASA-TENIS", "Masa tenisi masası", StockCategory.Sports, StockUnit.Piece, 2, null, null),
        ("SPR-RAKET", "Masa tenisi raketi", StockCategory.Sports, StockUnit.Piece, 8, null, null),
        ("SPR-FUTBOL", "Futbol topu", StockCategory.Sports, StockUnit.Piece, 6, null, null),
        ("SPR-BASKET", "Basketbol topu", StockCategory.Sports, StockUnit.Piece, 4, null, null),
        ("SPR-MINDER", "Jimnastik minderi", StockCategory.Sports, StockUnit.Piece, 6, null, null),
        ("SPR-FILE", "Voleybol filesi", StockCategory.Sports, StockUnit.Piece, 1, null, null),

        ("ATL-TORNA", "Seramik tornası", StockCategory.WorkshopAndScience, StockUnit.Piece, 1, null, null),
        ("ATL-FIRIN", "Seramik fırını", StockCategory.WorkshopAndScience, StockUnit.Piece, 1, null, null),
        ("ATL-BOYA", "Boya seti", StockCategory.WorkshopAndScience, StockUnit.Set, 8, null, "Atölye sarf"),
        ("ATL-ROBOT", "Robotik set", StockCategory.WorkshopAndScience, StockUnit.Set, 4, null, null),
        ("ATL-MIKRO", "Mikroskop", StockCategory.WorkshopAndScience, StockUnit.Piece, 4, null, null),
        ("ATL-3D", "3D yazıcı", StockCategory.WorkshopAndScience, StockUnit.Piece, 1, null, null),

        ("SRG-PANO", "Sergi panosu", StockCategory.ExhibitionAndArt, StockUnit.Piece, 6, null, null),
        ("SRG-KAIDE", "Sergi kaidesi", StockCategory.ExhibitionAndArt, StockUnit.Piece, 4, null, null),
        ("SRG-CERCEVE", "Çerçeve", StockCategory.ExhibitionAndArt, StockUnit.Piece, 10, null, null),
        ("SRG-TUVAL", "Tuval", StockCategory.ExhibitionAndArt, StockUnit.Piece, 10, null, null),

        ("MOB-SANDALYE", "Sandalye", StockCategory.Furniture, StockUnit.Piece, 40, null, "Salon oturma"),
        ("MOB-KATLANIR", "Katlanır sandalye", StockCategory.Furniture, StockUnit.Piece, 40, null, null),
        ("MOB-MASA", "Katlanır masa", StockCategory.Furniture, StockUnit.Piece, 8, null, null),
        ("MOB-KURSÜ", "Kürsü", StockCategory.Furniture, StockUnit.Piece, 1, null, null),
        ("MOB-DOLAP", "Malzeme dolabı", StockCategory.Furniture, StockUnit.Piece, 2, null, null),

        ("BIL-PROJ", "Projeksiyon cihazı", StockCategory.ItEquipment, StockUnit.Piece, 1, null, null),
        ("BIL-LAPTOP", "Dizüstü bilgisayar", StockCategory.ItEquipment, StockUnit.Piece, 2, null, null),
        ("BIL-EKRAN", "Ekran / monitör", StockCategory.ItEquipment, StockUnit.Piece, 2, null, null),
        ("BIL-YAZICI", "Yazıcı", StockCategory.ItEquipment, StockUnit.Piece, 1, null, null),
        ("BIL-KAMERA", "Kayıt kamerası", StockCategory.ItEquipment, StockUnit.Piece, 1, null, null),

        ("SARF-A4", "A4 kâğıt", StockCategory.Consumables, StockUnit.Pack, 10, null, null),
        ("SARF-TONER", "Toner", StockCategory.Consumables, StockUnit.Piece, 2, null, null),
        ("SARF-MARKER", "Tahta kalemi", StockCategory.Consumables, StockUnit.Pack, 4, null, null),

        ("IKR-CAY", "Çay makinesi", StockCategory.Catering, StockUnit.Piece, 1, null, null),
        ("IKR-SEBIL", "Su sebili", StockCategory.Catering, StockUnit.Piece, 1, null, null),
        ("IKR-TEPSI", "İkram tepsisi", StockCategory.Catering, StockUnit.Piece, 4, null, null),

        ("GUV-YANGIN", "Yangın tüpü", StockCategory.Safety, StockUnit.Piece, 4, null, null),
        ("GUV-ILKYARDIM", "İlk yardım çantası", StockCategory.Safety, StockUnit.Piece, 2, null, null),
        ("GUV-BARIYER", "Uyarı bariyeri", StockCategory.Safety, StockUnit.Piece, 6, null, null),

        ("DIS-HORTUM", "Bahçe hortumu", StockCategory.OutdoorAndPark, StockUnit.Meter, 20, null, null),
        ("DIS-BANK", "Park bankı", StockCategory.OutdoorAndPark, StockUnit.Piece, 4, null, null),
        ("DIS-CIM", "Çim biçme makinesi", StockCategory.OutdoorAndPark, StockUnit.Piece, 1, null, null)
    ];
}
