using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Settlements;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

internal static class SettlementSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        var path = ResolveGeoJsonPath(environment);
        if (path is null)
        {
            logger.LogWarning(
                "Seed: sehitkamil-mahalleler.geojson bulunamadı — yerleşimler atlandı. Kaynak: frontend/public/geo (ilçe ADM3).");
            return;
        }

        await using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("features", out var features))
        {
            logger.LogWarning("Seed: GeoJSON features yok ({Path}).", path);
            return;
        }

        var existing = await db.Settlements.IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.OfficialCode, ct);

        var added = 0;
        var updated = 0;
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var props))
                continue;
            var code = props.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var name = props.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                continue;

            var geometry = feature.TryGetProperty("geometry", out var geo)
                ? geo.GetRawText()
                : null;
            SettlementGeoHelper.TryCentroid(geometry, out var lat, out var lng);
            var isRural = name.Contains("KÖY", StringComparison.OrdinalIgnoreCase)
                || name.Contains("KOY", StringComparison.OrdinalIgnoreCase);
            var type = isRural ? "KirsalMahalle" : "Mahalle";

            if (existing.TryGetValue(code, out var row))
            {
                var changed = false;
                if (row.Name != name) { row.Name = name; changed = true; }
                if (row.GeometryJson != geometry) { row.GeometryJson = geometry; changed = true; }
                if (row.CentroidLat != lat) { row.CentroidLat = lat; changed = true; }
                if (row.CentroidLng != lng) { row.CentroidLng = lng; changed = true; }
                if (row.IsRural != isRural) { row.IsRural = isRural; changed = true; }
                if (row.SettlementType != type) { row.SettlementType = type; changed = true; }
                if (changed)
                {
                    row.UpdatedBy = "seed";
                    row.UpdatedAtUtc = DateTime.UtcNow;
                    updated++;
                }
                continue;
            }

            db.Settlements.Add(new Settlement
            {
                OfficialCode = code,
                Name = name,
                DisplayName = name,
                SettlementType = type,
                IsRural = isRural,
                CentroidLat = lat,
                CentroidLng = lng,
                GeometryJson = geometry,
                IsActive = true,
                CreatedBy = "seed"
            });
            added++;
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed: yerleşimler (kaynak={Path}) eklendi={Added}, güncellendi={Updated}.",
                path, added, updated);
        }

        await SeedOfficialPopulationsAsync(db, environment, logger, ct);
        await SeedHeadmenAsync(db, logger, ct);
        await SeedSchoolsAsync(db, logger, ct);
        await SeedAreasAsync(db, logger, ct);

        if (environment.IsDevelopment())
        {
            var hasOfficial = await db.SettlementPopulations.AnyAsync(x => x.IsOfficial, ct);
            if (!hasOfficial)
                await SeedSamplePopulationsAsync(db, logger, ct);
            await LinkEventsToSettlementsAsync(db, logger, ct);
            await BackfillEventMetaAsync(db, logger, ct);
        }
    }

    private static readonly string[] HeadmanFirstNames =
    [
        "Ahmet", "Mehmet", "Mustafa", "Ali", "Hüseyin", "Hasan", "İbrahim", "Osman",
        "Yusuf", "Murat", "Fatma", "Ayşe", "Emine", "Hatice", "Zeynep"
    ];

    private static readonly string[] HeadmanLastNames =
    [
        "Yıldız", "Kaya", "Demir", "Çelik", "Şahin", "Yılmaz", "Aydın", "Öztürk",
        "Arslan", "Doğan", "Kılıç", "Aslan", "Koç", "Kurt", "Özdemir"
    ];

    private static async Task SeedHeadmenAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var rows = await db.Settlements
            .Where(s => s.HeadmanPhone == null || s.HeadmanPhone == "" || s.HeadmanName == null || s.HeadmanName == "")
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        var updated = 0;
        foreach (var row in rows)
        {
            var hash = StableHash(row.OfficialCode + row.Name);
            row.HeadmanName = $"{HeadmanFirstNames[hash % HeadmanFirstNames.Length]} {HeadmanLastNames[(hash / 17) % HeadmanLastNames.Length]}";
            var rest = (hash % 10_000_000).ToString("D7", CultureInfo.InvariantCulture);
            row.HeadmanPhone = "0532" + rest;
            row.UpdatedBy = "seed";
            row.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} mahalle muhtarı dolduruldu.", updated);
    }

    private static readonly (string Settlement, string Name, string Type, int Students, string Principal, string Phone)[] NamedSchools =
    [
        ("Aktoprak", "Aktoprak İlkokulu", "İlkokul", 486, "Harun Yılmaz", "03423211420"),
        ("Aktoprak", "Aktoprak Ortaokulu", "Ortaokul", 412, "Sevilay Demir", "03423211421"),
        ("Aktoprak", "Aktoprak Anadolu Lisesi", "Lise", 538, "Mehmet Kaya", "03423211422"),
        ("Ali Nacar", "Ali Nacar İlkokulu", "İlkokul", 624, "Turgut Şahin", "03423211810"),
        ("Ali Nacar", "Ali Nacar Ortaokulu", "Ortaokul", 501, "İrem Çelik", "03423211811"),
        ("Başak", "Başak Mahallesi İlkokulu", "İlkokul", 712, "Mahmut Arslan", "03423222110"),
        ("Başak", "Başak Ortaokulu", "Ortaokul", 588, "Adile Yıldız", "03423222111"),
        ("Belkıs", "Belkıs İlkokulu", "İlkokul", 394, "Sara Öztürk", "03423233120"),
        ("Belkıs", "Belkıs Ortaokulu", "Ortaokul", 341, "Mustafa Aydın", "03423233121"),
        ("Eyüpsultan", "Eyüpsultan İlkokulu", "İlkokul", 802, "Erol Doğan", "03423244130"),
        ("Eyüpsultan", "Eyüpsultan İmam Hatip Ortaokulu", "İmam Hatip", 356, "Neslihan Kılıç", "03423244131"),
        ("Kuzeyşehir", "Kuzeyşehir İlkokulu", "İlkokul", 918, "Nuriye Beyaz", "03423255140"),
        ("Kuzeyşehir", "Kuzeyşehir Ortaokulu", "Ortaokul", 744, "İbrahim Halil Koç", "03423255141"),
        ("Kuzeyşehir", "Kuzeyşehir Anadolu Lisesi", "Lise", 690, "Mustafa Teoman Kurt", "03423255142"),
        ("Mehmet Akif Ersoy", "Mehmet Akif Ersoy İlkokulu", "İlkokul", 845, "Hasan Yılmaz", "03423266150"),
        ("Mehmet Akif Ersoy", "Mehmet Akif Ersoy Ortaokulu", "Ortaokul", 671, "Erkan Genç", "03423266151"),
        ("Merveşehir", "Merveşehir İlkokulu", "İlkokul", 733, "Mehmet Emin Arslan", "03423277160"),
        ("Merveşehir", "Merveşehir Ortaokulu", "Ortaokul", 602, "Ziya Tekin", "03423277161"),
        ("Mütercim Asım", "Mütercim Asım İlkokulu", "İlkokul", 518, "Kemal Kartal", "03423288170"),
        ("Mütercim Asım", "Mütercim Asım Ortaokulu", "Ortaokul", 447, "Mehmet Yağmur", "03423288171"),
        ("Nurtepe", "Nurtepe İlkokulu", "İlkokul", 389, "Fatma Ekici", "03423299180"),
        ("Nurtepe", "Nurtepe Anaokulu", "Anaokulu", 126, "Ayşe Şen", "03423299181"),
        ("Karataş", "Karataş İlkokulu", "İlkokul", 964, "Ahmet Oral", "03422101320"),
        ("Karataş", "Karataş Anadolu Lisesi", "Lise", 812, "Zeynep Özdemir", "03422101321"),
        ("15 Temmuz", "15 Temmuz Şehitleri İlkokulu", "İlkokul", 1086, "Mustafa Çelik", "03422102410"),
        ("15 Temmuz", "15 Temmuz Ortaokulu", "Ortaokul", 874, "Emine Şahin", "03422102411"),
        ("İncili", "İncili Pınar İlkokulu", "İlkokul", 541, "Ali Demir", "03422103510"),
        ("İncirli", "İncirli İlkokulu", "İlkokul", 541, "Ali Demir", "03422103510"),
        ("Pirsultan", "Pirsultan İlkokulu", "İlkokul", 428, "Arzu Doğru", "03423301240"),
        ("Pirsultan", "Pirsultan Ortaokulu", "Ortaokul", 361, "Özlem Sağlam", "03423301241"),
        ("Zeytinli", "Zeytinli İlkokulu", "İlkokul", 477, "Tuğba Çabar", "03423312350"),
        ("Zeytinli", "Zeytinli Ortaokulu", "Ortaokul", 392, "Murat İpek", "03423312351"),
        ("Oktay Yalçın", "Oktay Yalçın İlkokulu", "İlkokul", 655, "Murat Kartal", "03423323460"),
        ("Oktay Yalçın", "Oktay Yalçın Ortaokulu", "Ortaokul", 519, "Fatih Bay", "03423323461"),
    ];

    private static async Task SeedSchoolsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var settlements = await db.Settlements
            .Select(s => new { s.Id, s.Name, s.IsRural, s.OfficialCode })
            .ToListAsync(ct);
        if (settlements.Count == 0)
            return;

        var pops = await db.SettlementPopulations.AsNoTracking().ToListAsync(ct);
        var popById = pops
            .GroupBy(p => p.SettlementId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Year).First());

        var existing = await db.SettlementSchools.ToListAsync(ct);
        var bySettleName = existing
            .GroupBy(x => x.SettlementId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => FoldName(x.Name), x => x, StringComparer.Ordinal));

        var byFold = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var s in settlements)
        {
            var key = FoldName(s.Name);
            if (key.Length > 0)
                byFold.TryAdd(key, s.Id);
        }

        var added = 0;
        var updated = 0;

        foreach (var s in settlements)
        {
            popById.TryGetValue(s.Id, out var pop);
            var population = pop?.Population ?? 0;
            var hash = StableHash(s.OfficialCode + s.Name);
            var pretty = PrettyPlace(s.Name);
            if (!bySettleName.TryGetValue(s.Id, out var named))
            {
                named = new Dictionary<string, SettlementSchool>(StringComparer.Ordinal);
                bySettleName[s.Id] = named;
            }

            foreach (var plan in PlannedSchoolsFor(s.IsRural, pretty, population, hash))
                UpsertSchool(db, named, s.Id, plan.Name, plan.Type, plan.Students, plan.Principal, plan.Phone, ref added, ref updated);
        }

        foreach (var extra in NamedSchools)
        {
            var fold = FoldName(extra.Settlement);
            if (!byFold.TryGetValue(fold, out var sid))
                continue;
            if (!bySettleName.TryGetValue(sid, out var named))
            {
                named = new Dictionary<string, SettlementSchool>(StringComparer.Ordinal);
                bySettleName[sid] = named;
            }

            UpsertSchool(db, named, sid, extra.Name, extra.Type, extra.Students, extra.Principal, extra.Phone, ref added, ref updated);
        }

        if (added == 0 && updated == 0)
        {
            logger.LogInformation("Seed: mahalle okulları zaten güncel.");
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: mahalle okulları eklendi={Added}, güncellendi={Updated}.", added, updated);
    }

    private static void UpsertSchool(
        AppDbContext db,
        Dictionary<string, SettlementSchool> named,
        Guid settlementId,
        string name,
        string type,
        int students,
        string principal,
        string phone,
        ref int added,
        ref int updated)
    {
        var key = FoldName(name);
        if (key.Length == 0)
            return;

        if (named.TryGetValue(key, out var row))
        {
            if (row.CreatedBy != "seed")
                return;
            var changed = false;
            if (row.Name != name) { row.Name = name; changed = true; }
            if (row.SchoolType != type) { row.SchoolType = type; changed = true; }
            if (row.StudentCount != students) { row.StudentCount = students; changed = true; }
            if (row.PrincipalName != principal) { row.PrincipalName = principal; changed = true; }
            if (row.PrincipalPhone != phone) { row.PrincipalPhone = phone; changed = true; }
            if (!changed)
                return;
            row.UpdatedBy = "seed";
            row.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
            return;
        }

        var created = new SettlementSchool
        {
            SettlementId = settlementId,
            Name = name,
            SchoolType = type,
            StudentCount = students,
            PrincipalName = principal,
            PrincipalPhone = phone,
            CreatedBy = "seed"
        };
        db.SettlementSchools.Add(created);
        named[key] = created;
        added++;
    }

    private readonly record struct SchoolPlan(string Name, string Type, int Students, string Principal, string Phone);

    private static List<SchoolPlan> PlannedSchoolsFor(bool rural, string pretty, int population, int hash)
    {
        var list = new List<SchoolPlan>();
        var pop = population > 0 ? population : 800 + hash % 4000;

        void Add(string suffix, string type, double ratio, int min, int max, int offset)
        {
            var students = Math.Clamp((int)Math.Round(pop * ratio), min, max);
            var h = hash + offset;
            var principal = $"{HeadmanFirstNames[h % HeadmanFirstNames.Length]} {HeadmanLastNames[(h / 13) % HeadmanLastNames.Length]}";
            var rest = ((h % 9_000_000) + 1_000_000).ToString("D7", CultureInfo.InvariantCulture);
            list.Add(new SchoolPlan($"{pretty} {suffix}", type, students, principal, "0342" + rest));
        }

        if (rural)
        {
            Add("İlkokulu", "İlkokul", 0.08, 28, 220, 3);
            if (hash % 3 == 0)
                Add("Ortaokulu", "Ortaokul", 0.05, 24, 160, 11);
            return list;
        }

        Add("Anaokulu", "Anaokulu", 0.018, 42, 260, 2);
        Add("İlkokulu", "İlkokul", 0.055, 90, 1200, 5);
        Add("Ortaokulu", "Ortaokul", 0.042, 80, 980, 9);
        if (pop >= 3500 || hash % 4 == 0)
            Add("Anadolu Lisesi", "Lise", 0.035, 140, 1100, 17);
        if (hash % 7 == 0)
            Add("İmam Hatip Ortaokulu", "İmam Hatip", 0.02, 80, 520, 23);
        return list;
    }

    private static readonly (string Settlement, string Name, string Type, string? Note)[] NamedAreas =
    [
        ("Aktoprak", "Aktoprak Millet Bahçesi", "Park", "Açık yeşil alan"),
        ("Aktoprak", "Aktoprak Halı Saha", "Spor alanı", null),
        ("15 Temmuz", "15 Temmuz Milli İrade Meydanı", "Meydan", "Açık etkinlik alanı"),
        ("Karataş", "Karataş Millet Bahçesi", "Park", null),
        ("Dülükbaba", "Dülükbaba Mesire Alanı", "Yeşil alan", "Piknik ve mesire"),
        ("Belkız", "Belkız Millet Bahçesi", "Park", null),
        ("Kuzeyşehir", "Kuzeyşehir Spor Sahası", "Spor alanı", null),
        ("Merveşehir", "Merveşehir Mahalle Parkı", "Park", null),
        ("Eyüpsultan", "Eyüpsultan Çocuk Oyun Alanı", "Çocuk oyun alanı", null),
        ("Pirsultan", "Pirsultan Açık Etkinlik Alanı", "Açık etkinlik alanı", null),
        ("Zeytinli", "Zeytinli Açık Etkinlik Alanı", "Açık etkinlik alanı", null),
        ("Gazikent", "Gazikent Parkı", "Park", null),
        ("Batıkent", "Batıkent Millet Bahçesi", "Park", null),
        ("Karşıyaka", "Karşıyaka Mahalle Parkı", "Park", null),
        ("Seyrantepe", "Seyrantepe Spor Sahası", "Spor alanı", null),
    ];

    private static async Task SeedAreasAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var settlements = await db.Settlements
            .Select(s => new { s.Id, s.Name, s.IsRural, s.OfficialCode })
            .ToListAsync(ct);
        if (settlements.Count == 0)
            return;

        var existing = await db.SettlementAreas.ToListAsync(ct);
        var bySettleName = existing
            .GroupBy(x => x.SettlementId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => FoldName(x.Name), x => x, StringComparer.Ordinal));

        var byFold = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var s in settlements)
        {
            var key = FoldName(s.Name);
            if (key.Length > 0)
                byFold.TryAdd(key, s.Id);
        }

        var added = 0;
        var updated = 0;

        foreach (var s in settlements)
        {
            var hash = StableHash("area:" + s.OfficialCode + s.Name);
            var pretty = PrettyPlace(s.Name);
            if (!bySettleName.TryGetValue(s.Id, out var named))
            {
                named = new Dictionary<string, SettlementArea>(StringComparer.Ordinal);
                bySettleName[s.Id] = named;
            }

            foreach (var plan in PlannedAreasFor(s.IsRural, pretty, hash))
                UpsertArea(db, named, s.Id, plan.Name, plan.Type, plan.Note, ref added, ref updated);
        }

        foreach (var extra in NamedAreas)
        {
            var fold = FoldName(extra.Settlement);
            if (!byFold.TryGetValue(fold, out var sid))
                continue;
            if (!bySettleName.TryGetValue(sid, out var named))
            {
                named = new Dictionary<string, SettlementArea>(StringComparer.Ordinal);
                bySettleName[sid] = named;
            }

            UpsertArea(db, named, sid, extra.Name, extra.Type, extra.Note, ref added, ref updated);
        }

        if (added == 0 && updated == 0)
        {
            logger.LogInformation("Seed: mahalle alanları zaten güncel.");
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: mahalle alanları eklendi={Added}, güncellendi={Updated}.", added, updated);
    }

    private static void UpsertArea(
        AppDbContext db,
        Dictionary<string, SettlementArea> named,
        Guid settlementId,
        string name,
        string type,
        string? note,
        ref int added,
        ref int updated)
    {
        var key = FoldName(name);
        if (key.Length == 0)
            return;

        if (named.TryGetValue(key, out var row))
        {
            if (row.CreatedBy != "seed")
                return;
            var changed = false;
            if (row.Name != name) { row.Name = name; changed = true; }
            if (row.AreaType != type) { row.AreaType = type; changed = true; }
            if (row.Note != note) { row.Note = note; changed = true; }
            if (!changed)
                return;
            row.UpdatedBy = "seed";
            row.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
            return;
        }

        var created = new SettlementArea
        {
            SettlementId = settlementId,
            Name = name,
            AreaType = type,
            Note = note,
            CreatedBy = "seed"
        };
        db.SettlementAreas.Add(created);
        named[key] = created;
        added++;
    }

    private readonly record struct AreaPlan(string Name, string Type, string? Note);

    private static List<AreaPlan> PlannedAreasFor(bool rural, string pretty, int hash)
    {
        var list = new List<AreaPlan>();
        if (rural)
        {
            list.Add(new AreaPlan($"{pretty} Köy Meydanı", "Meydan", "Açık toplanma alanı"));
            if (hash % 2 == 0)
                list.Add(new AreaPlan($"{pretty} Spor Sahası", "Spor alanı", null));
            return list;
        }

        list.Add(new AreaPlan($"{pretty} Mahalle Parkı", "Park", "Belediye yeşil alanı"));
        if (hash % 3 == 0)
            list.Add(new AreaPlan($"{pretty} Çocuk Oyun Alanı", "Çocuk oyun alanı", null));
        if (hash % 4 == 0)
            list.Add(new AreaPlan($"{pretty} Spor Sahası", "Spor alanı", null));
        if (hash % 5 == 0)
            list.Add(new AreaPlan($"{pretty} Meydanı", "Meydan", "Açık etkinlik alanı"));
        return list;
    }

    private static string PrettyPlace(string name)
    {
        var s = Regex.Replace(name.Trim(), @"\s*MAHALLES[İI]\s*$", string.Empty, RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*KÖYÜ\s*$", " Köyü", RegexOptions.IgnoreCase);
        return CultureInfo.GetCultureInfo("tr-TR").TextInfo.ToTitleCase(s.ToLower(new CultureInfo("tr-TR")));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
                hash = hash * 31 + ch;
            return hash == int.MinValue ? 0 : Math.Abs(hash);
        }
    }

    private sealed class AdnksFile
    {
        public int Year { get; set; }
        public string Source { get; set; } = "TUIK_ADNKS";
        public string? SourceReference { get; set; }
        public List<AdnksItem> Items { get; set; } = [];
    }

    private sealed class AdnksItem
    {
        public string Name { get; set; } = string.Empty;
        public int Population { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
    }

    private static async Task SeedOfficialPopulationsAsync(
        AppDbContext db,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        var path = ResolveAdnksPath(environment);
        if (path is null)
        {
            logger.LogWarning("Seed: sehitkamil-adnks-2025.json bulunamadı — resmi nüfus atlandı.");
            return;
        }

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<AdnksFile>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ct);
        if (file?.Items is not { Count: > 0 })
            return;

        var byFold = new Dictionary<string, AdnksItem>(StringComparer.Ordinal);
        foreach (var item in file.Items)
        {
            var key = FoldName(item.Name);
            if (key.Length == 0) continue;
            byFold.TryAdd(key, item);
        }

        var settlements = await db.Settlements.ToListAsync(ct);
        var year = file.Year > 0 ? file.Year : 2025;
        var source = string.IsNullOrWhiteSpace(file.Source) ? "TUIK_ADNKS" : file.Source;
        var sourceRef = string.IsNullOrWhiteSpace(file.SourceReference)
            ? "TÜİK ADNKS 31.12.2025"
            : file.SourceReference;

        var existing = await db.SettlementPopulations
            .Where(p => p.Year == year)
            .ToListAsync(ct);
        var bySettlement = existing.ToDictionary(p => p.SettlementId);

        var matchedIds = new HashSet<Guid>();
        var matched = 0;
        var updated = 0;
        var added = 0;

        foreach (var s in settlements)
        {
            if (!byFold.TryGetValue(FoldName(s.Name), out var item))
                continue;
            if (item.MaleCount + item.FemaleCount != item.Population)
                continue;

            matched++;
            matchedIds.Add(s.Id);
            if (bySettlement.TryGetValue(s.Id, out var row))
            {
                var changed =
                    row.Population != item.Population
                    || row.MaleCount != item.MaleCount
                    || row.FemaleCount != item.FemaleCount
                    || row.IsOfficial != true
                    || row.Source != source;
                if (!changed) continue;
                row.Population = item.Population;
                row.MaleCount = item.MaleCount;
                row.FemaleCount = item.FemaleCount;
                row.ChildCount = null;
                row.Source = source;
                row.SourceReference = sourceRef;
                row.IsOfficial = true;
                row.UpdatedBy = "seed";
                row.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.SettlementPopulations.Add(new SettlementPopulation
                {
                    SettlementId = s.Id,
                    Year = year,
                    Population = item.Population,
                    MaleCount = item.MaleCount,
                    FemaleCount = item.FemaleCount,
                    Source = source,
                    SourceReference = sourceRef,
                    IsOfficial = true,
                    CreatedBy = "seed"
                });
                added++;
            }
        }

        var leftovers = existing
            .Where(p => p.Source == "LOCAL_DEV_SAMPLE" && !matchedIds.Contains(p.SettlementId))
            .ToList();
        if (leftovers.Count > 0)
            db.SettlementPopulations.RemoveRange(leftovers);

        if (added == 0 && updated == 0 && leftovers.Count == 0)
        {
            logger.LogInformation("Seed: ADNKS nüfus zaten güncel (eşleşen={Matched}).", matched);
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed: ADNKS {Year} nüfus eklendi={Added}, güncellendi={Updated}, örnek silindi={Removed}, eşleşen mahalle={Matched}/{Total}.",
            year, added, updated, leftovers.Count, matched, settlements.Count);
    }

    private static string FoldName(string name)
    {
        var s = name.ToUpper(new CultureInfo("tr-TR"));
        s = s.Replace("Ç", "C", StringComparison.Ordinal)
            .Replace("Ğ", "G", StringComparison.Ordinal)
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("Ö", "O", StringComparison.Ordinal)
            .Replace("Ş", "S", StringComparison.Ordinal)
            .Replace("Ü", "U", StringComparison.Ordinal)
            .Replace("Â", "A", StringComparison.Ordinal);
        s = Regex.Replace(s, @"MAHALLESI|MAHALLE|KOYU|KOY", string.Empty, RegexOptions.CultureInvariant);
        return Regex.Replace(s, @"[^A-Z0-9]", string.Empty);
    }

    private static async Task SeedSamplePopulationsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.SettlementPopulations.AnyAsync(x => x.IsOfficial, ct))
            return;
        var settlements = await db.Settlements.AsNoTracking()
            .Select(s => new { s.Id, s.OfficialCode, s.Name })
            .ToListAsync(ct);
        if (settlements.Count == 0)
            return;

        var existing = await db.SettlementPopulations
            .Where(p => p.Source == "LOCAL_DEV_SAMPLE")
            .ToListAsync(ct);
        var bySettlement = existing.ToDictionary(p => p.SettlementId);
        var added = 0;
        var filled = 0;

        foreach (var s in settlements)
        {
            SplitSamplePopulation(s.OfficialCode, out var total, out var child, out var female, out var male);
            if (bySettlement.TryGetValue(s.Id, out var row))
            {
                if (row.MaleCount is null && row.FemaleCount is null && row.ChildCount is null)
                {
                    row.MaleCount = male;
                    row.FemaleCount = female;
                    row.ChildCount = child;
                    if (row.Population <= 0) row.Population = total;
                    row.UpdatedBy = "seed";
                    row.UpdatedAtUtc = DateTime.UtcNow;
                    filled++;
                }
                continue;
            }

            db.SettlementPopulations.Add(new SettlementPopulation
            {
                SettlementId = s.Id,
                Year = 2025,
                Population = total,
                ChildCount = child,
                FemaleCount = female,
                MaleCount = male,
                Source = "LOCAL_DEV_SAMPLE",
                SourceReference = "Geliştirme örneği — resmi ADNKS değil",
                IsOfficial = false,
                CreatedBy = "seed"
            });
            added++;
        }

        if (added == 0 && filled == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed: örnek nüfus eklendi={Added}, çocuk/kadın/erkek dolduruldu={Filled} (LOCAL_DEV_SAMPLE, 2025).",
            added, filled);
    }

    private static void SplitSamplePopulation(
        string officialCode,
        out int total,
        out int child,
        out int female,
        out int male)
    {
        var hash = 0;
        foreach (var ch in officialCode)
            hash = (hash * 33) ^ ch;
        total = 420 + Math.Abs(hash % 2800);
        var childPct = 18 + Math.Abs(hash % 10);
        child = total * childPct / 100;
        var remaining = total - child;
        var femaleShare = 48 + Math.Abs((hash / 7) % 7);
        female = remaining * femaleShare / 100;
        male = remaining - female;
    }

    private static async Task LinkEventsToSettlementsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.EventSettlements.AnyAsync(ct))
            return;

        var events = await db.Events
            .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Completed)
            .ToListAsync(ct);
        var settlements = await db.Settlements
            .Where(s => s.GeometryJson != null)
            .Select(s => new { s.Id, s.GeometryJson })
            .ToListAsync(ct);
        if (events.Count == 0 || settlements.Count == 0)
            return;

        var added = 0;
        foreach (var ev in events)
        {
            if (ev.Latitude is not double lat || ev.Longitude is not double lng)
                continue;

            var hit = settlements.FirstOrDefault(s =>
                SettlementGeoHelper.Contains(s.GeometryJson, lat, lng));
            if (hit is null)
                continue;

            var attendance = ev.ExpectedAttendees
                ?? (80 + Math.Abs(ev.Title.GetHashCode() % 220));
            if (ev.ExpectedAttendees is null)
            {
                ev.ExpectedAttendees = attendance;
                ev.UpdatedBy = "seed";
            }

            if (string.IsNullOrWhiteSpace(ev.Category))
            {
                ev.Category = GuessCategory(ev.Title);
                ev.UpdatedBy = "seed";
            }

            db.EventSettlements.Add(new EventSettlement
            {
                EventId = ev.Id,
                SettlementId = hit.Id,
                AttendanceCount = attendance,
                UniqueBeneficiaryCount = attendance,
                CreatedBy = "seed"
            });
            added++;
        }

        if (added == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} etkinlik yerleşime bağlandı (PIP).", added);
    }

    private static async Task BackfillEventMetaAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var events = await db.Events
            .Where(e => e.CreatedBy == "seed" && (e.Category == null || e.Category == "" || e.Category == EventCategories.Other))
            .ToListAsync(ct);
        foreach (var ev in events)
        {
            var guessed = GuessCategory(ev.Title);
            if (ev.Category == guessed)
                continue;
            ev.Category = guessed;
            ev.UpdatedBy = "seed";
        }

        var links = await db.EventSettlements
            .Where(x => x.UniqueBeneficiaryCount == null && x.AttendanceCount > 0)
            .ToListAsync(ct);
        foreach (var link in links)
        {
            link.UniqueBeneficiaryCount = link.AttendanceCount;
            link.UpdatedBy = "seed";
        }

        if (events.Count == 0 && links.Count == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed: etkinlik meta dolduruldu (tür={Cat}, ulaşılan kişi={Unq}).",
            events.Count, links.Count);
    }

    internal static string GuessCategory(string title)
    {
        var t = title.ToLocaleLower();
        if (t.Contains("tiyatro") || t.Contains("konser") || t.Contains("sergi") || t.Contains("sinema") || t.Contains("resital"))
            return EventCategories.Culture;
        if (t.Contains("spor") || t.Contains("şenlik") || t.Contains("senlik"))
            return EventCategories.Sports;
        if (t.Contains("çocuk") || t.Contains("cocuk") || t.Contains("gençlik") || t.Contains("genclik") || t.Contains("kütüphane") || t.Contains("kutuphane") || t.Contains("okuma"))
            return EventCategories.Youth;
        if (t.Contains("okul") || t.Contains("kodlama") || t.Contains("bilim") || t.Contains("seminer"))
            return EventCategories.Education;
        if (t.Contains("nikah") || t.Contains("buluşma") || t.Contains("bulusma"))
            return EventCategories.SocialSupport;
        return EventCategories.Other;
    }

    private static string ToLocaleLower(this string value) =>
        value.ToLower(new System.Globalization.CultureInfo("tr-TR"));

    private static string? ResolveGeoJsonPath(IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "Data", "sehitkamil-mahalleler.geojson"),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "..", "frontend", "public", "geo", "sehitkamil-mahalleler.geojson")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data", "sehitkamil-mahalleler.geojson"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveAdnksPath(IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "Data", "sehitkamil-adnks-2025.json"),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "Data", "sehitkamil-adnks-2025.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data", "sehitkamil-adnks-2025.json")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
