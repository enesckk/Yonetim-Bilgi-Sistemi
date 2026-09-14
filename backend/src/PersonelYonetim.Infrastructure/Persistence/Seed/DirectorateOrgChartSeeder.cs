using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>
/// Kültür, Sanat ve Sosyal İşler müdürlük şeması (PDF) — birim, tesis ve kadro.
/// Idempotent: isimle eşler, mevcut kaydı günceller.
/// </summary>
internal static class DirectorateOrgChartSeeder
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private const string HistoricalImport = "historical-roster-2026";

    // The development seed also rewrites existing records and creates demo events.
    // Production import is deliberately insert-only for people and conservative for units.
    public static async Task ImportHistoricalRosterAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () => await ImportHistoricalRosterCoreAsync(db, logger, ct));
    }

    private static async Task ImportHistoricalRosterCoreAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var roster = Roster();
        if (roster.Length != 172 || roster.Select(p => PersonKey(p.First, p.Last)).Distinct().Count() != roster.Length)
            throw new InvalidOperationException("Historical roster must contain exactly 172 unique people.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var units = await db.OrganizationUnits.IgnoreQueryFilters()
            .Where(x => x.Code != null).ToDictionaryAsync(x => x.Code!, ct);
        if (!units.ContainsKey("KSSIM") || !units.ContainsKey("BY"))
            throw new InvalidOperationException("Historical roster requires the base organization.");
        var categories = await db.FacilityCategories.Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, ct);

        async Task<OrganizationUnit> EnsureUnit(string code, string name, OrganizationUnitType type,
            string parentCode, string? categoryCode = null, OrganizationUnitStatus status = OrganizationUnitStatus.Active,
            double? latitude = null, double? longitude = null, int? capacity = null)
        {
            if (!units.TryGetValue(parentCode, out var parent) || parent.IsDeleted)
                throw new InvalidOperationException($"Missing active parent unit: {parentCode}");
            if (!units.TryGetValue(code, out var unit))
            {
                unit = new OrganizationUnit
                {
                    Code = code, Name = name, Type = type, ParentId = parent.Id, Status = status,
                    FacilityCategoryId = categoryCode is null ? null : categories[categoryCode],
                    Latitude = latitude, Longitude = longitude, Capacity = capacity,
                    CreatedBy = HistoricalImport
                };
                db.OrganizationUnits.Add(unit);
                units.Add(code, unit);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                if (unit.IsDeleted)
                    throw new InvalidOperationException($"Historical unit was previously deleted: {code}");
                // Only enrich untouched base seed units. Never rewrite user-managed units.
                if (unit.CreatedBy == "seed" && unit.UpdatedBy is null)
                {
                    unit.Type = type;
                    unit.ParentId = parent.Id;
                    unit.FacilityCategoryId ??= categoryCode is null ? null : categories[categoryCode];
                    unit.Latitude ??= latitude;
                    unit.Longitude ??= longitude;
                    unit.Capacity ??= capacity;
                    unit.UpdatedBy = HistoricalImport;
                }
            }
            return unit;
        }

        await EnsureUnit("KKM", "Şehitkamil Kültür ve Kongre Merkezi", OrganizationUnitType.Facility,
            "KSSIM", "KULTUR_MERKEZI", latitude: 37.0785, longitude: 37.3720);
        foreach (var hall in new (string Code, string Name, int Capacity)[]
        {
            ("KKM_MEHMET_AKIF", "Mehmet Akif Salonu", 1070),
            ("KKM_OAS", "Ömer Asım Aksoy Salonu", 300),
            ("KKM_MUNIF", "Münif Paşa Salonu", 300),
            ("KKM_NURI", "Nuri Paşa Salonu", 300),
            ("KKM_MUTERIM", "Mütercim Asım Salonu", 70),
            ("KKM_SERGI", "Sergi Salonu", 0)
        })
            await EnsureUnit(hall.Code, hall.Name, OrganizationUnitType.Facility, "KKM",
                "KULTUR_MERKEZI", latitude: 37.0785, longitude: 37.3720,
                capacity: hall.Capacity == 0 ? null : hall.Capacity);

        foreach (var site in new (string Code, string Name, string Category, double Lat, double Lng)[]
        {
            ("SANAT", "Şehitkamil Sanat Merkezi", "KULTUR_MERKEZI", 37.0912, 37.3515),
            ("NIKAH", "Şehitkamil Nikah Salonu", "SOSYAL_TESIS", 37.0850, 37.3650),
            ("DTSS", "Devlet Tiyatroları Şehitkamil Sahnesi", "KULTUR_MERKEZI", 37.0740, 37.3810),
            ("SAMI", "M. Sami Benli Kütüphanesi", "KUTUPHANE", 37.0890, 37.3600),
            ("BILIM", "Bilim Şehitkamil", "GENCLIK_MERKEZI", 37.0955, 37.3400),
            ("AGROPARK", "Agropark", "SOSYAL_TESIS", 37.1860, 37.2860)
        })
            await EnsureUnit(site.Code, site.Name, OrganizationUnitType.Facility, "KSSIM",
                site.Category, latitude: site.Lat, longitude: site.Lng);

        await EnsureUnit("GENCLIK_KUT", "Gençlik Kütüphaneleri", OrganizationUnitType.SubUnit,
            "KSSIM", "KUTUPHANE");
        await EnsureUnit("GEZI", "Kültürel Geziler", OrganizationUnitType.MainUnit, "KSSIM");
        await EnsureUnit("SAMI_COCUK", "M. Sami Benli Kütüphanesi Çocuk Kütüphanesi",
            OrganizationUnitType.SubUnit, "SAMI", "KUTUPHANE");
        await EnsureUnit("IDARI", "İdari Büro", OrganizationUnitType.SubUnit, "DTSS", "IDARI_BINA");

        foreach (var library in new (string Code, string Name, OrganizationUnitStatus Status, double Lat, double Lng)[]
        {
            ("GK_AKTOPRAK", "Aktoprak Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1865, 37.2870),
            ("GK_ALINACAR", "Ali Nacar Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1200, 37.3400),
            ("GK_BASAK", "Başak Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1300, 37.3500),
            ("GK_BELKIS", "Belkıs Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1400, 37.3000),
            ("GK_EYUPSULTAN", "Eyüpsultan Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1100, 37.3700),
            ("GK_KUZEYSEHIR", "Kuzeyşehir Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1500, 37.3300),
            ("GK_MAE", "Mehmet Akif Ersoy Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1000, 37.3450),
            ("GK_MERVE", "Merveşehir Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1250, 37.3550),
            ("GK_MUTERIM", "Mütercim Asım Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1050, 37.3650),
            ("GK_NURTEPE", "Nurtepe Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1150, 37.3250),
            ("GK_OKTAY", "Oktay Yalçın Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1350, 37.3350),
            ("GK_PIRSULTAN", "Pirsultan Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1450, 37.3150),
            ("GK_ZEYTINLI", "Zeytinli ŞESEM", OrganizationUnitStatus.Active, 37.1600, 37.3200),
            ("GK_AKSU", "Abdulkadir Aksu Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1180, 37.3480),
            ("GK_NFK", "Necip Fazıl Kısakürek Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1220, 37.3380),
            ("GK_YESILOVA", "Yeşilova Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1280, 37.3280),
            ("GK_KARACAGOLAN", "Karacaoğlan Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1320, 37.3180),
            ("GK_KOCATEPE", "Kocatepe Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1360, 37.3080),
            ("GK_SIRINEVLER", "Şirinevler Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1400, 37.2980),
            ("GK_SEYRANTEPE", "Seyrantepe Gençlik Kütüphanesi", OrganizationUnitStatus.UnderRenovation, 37.1480, 37.3420),
            ("GK_ERBAKAN", "Necmettin Erbakan Gençlik Kütüphanesi", OrganizationUnitStatus.UnderRenovation, 37.1520, 37.3520)
        })
            await EnsureUnit(library.Code, library.Name, OrganizationUnitType.Facility,
                "GENCLIK_KUT", "KUTUPHANE", library.Status, library.Lat, library.Lng);

        var titles = await EnsureTitlesAsync(db, ct);
        var duties = await EnsureDutiesAsync(db, ct, preserveExisting: true);
        var employmentTypes = await db.EmploymentTypes.Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, ct);
        var existing = await db.Employees.IgnoreQueryFilters().ToListAsync(ct);
        var byName = existing.Select(e => PersonKey(e.FirstName, e.LastName))
            .ToHashSet(StringComparer.Ordinal);
        var byNumber = existing.Where(e => e.EmployeeNumber != null)
            .Select(e => e.EmployeeNumber!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new Dictionary<string, Employee>(StringComparer.Ordinal);
        var skipped = 0;
        for (var i = 0; i < roster.Length; i++)
        {
            var row = roster[i];
            var key = PersonKey(row.First, row.Last);
            if (byName.Contains(key)) { skipped++; continue; }
            var number = $"HR-2026-{i + 1:000}";
            if (byNumber.Contains(number))
                throw new InvalidOperationException($"Historical employee number collision: {number}");
            if (!units.TryGetValue(row.Unit, out var unit) || unit.IsDeleted ||
                (row.Fac is not null && (!units.TryGetValue(row.Fac, out var fac) || fac.IsDeleted)))
                throw new InvalidOperationException($"Historical roster has an unavailable unit/facility: {row.Unit}/{row.Fac}");
            var employee = new Employee
            {
                FirstName = row.First, LastName = row.Last, EmployeeNumber = number,
                UnitId = unit.Id, FacilityId = row.Fac is null ? null : units[row.Fac].Id,
                EmploymentTypeId = employmentTypes[row.Memur ? "MEMUR" : "SEKABEL"],
                JobTitleId = titles[row.Title].Id,
                Status = EmployeeStatus.Active, Gender = Gender.Unspecified,
                CreatedBy = HistoricalImport
            };
            db.Employees.Add(employee);
            added.Add(key, employee);
        }
        await db.SaveChangesAsync(ct);
        foreach (var row in roster)
        {
            if (!added.TryGetValue(PersonKey(row.First, row.Last), out var employee)) continue;
            db.EmployeeAssignments.Add(new EmployeeAssignment
            {
                EmployeeId = employee.Id, JobDutyId = duties[row.Duty].Id, IsPrimary = true,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Description = "Aktarım tarihi; gerçek görev başlangıç tarihi kaynakta yok.",
                CreatedBy = HistoricalImport
            });
        }
        await db.SaveChangesAsync(ct);
        foreach (var (code, first, last) in new (string Code, string First, string Last)[]
        {
            ("BY", "Hakan", "Aslansoy"), ("KSSIM", "Zeynep", "Özbahçivan"),
            ("KKM", "Ahmet", "Oral"), ("SANAT", "Erhan Bozo", "Sarıkaya"),
            ("NIKAH", "Tutku", "Yapıcı"), ("DTSS", "Tarık", "Öndül"),
            ("GENCLIK_KUT", "Seydi Vakkas", "Cengiz"),
            ("GK_AKTOPRAK", "Harun", "Terlemez"), ("GK_ALINACAR", "Turgut", "Bozgeyik"),
            ("GK_BASAK", "Mahmut", "Orhan"), ("GK_BELKIS", "Sara", "Akkaya"),
            ("GK_EYUPSULTAN", "Erol", "Aras"), ("GK_KUZEYSEHIR", "Nuriye", "Beyaz"),
            ("GK_MAE", "Hasan", "Yılmaz"), ("GK_MERVE", "Mehmet Emin", "Arslan"),
            ("GK_MUTERIM", "Kemal", "Kartal"), ("GK_NURTEPE", "Fatma", "Ekici"),
            ("GK_OKTAY", "Murat", "Kartal"), ("GK_PIRSULTAN", "Arzu", "Doğru"),
            ("GK_ZEYTINLI", "Tuğba", "Çabar"), ("BILIM", "Eyüp", "Yenikomşu"),
            ("AGROPARK", "İrem", "Ölmez")
        })
        {
            if (units[code].ManagerEmployeeId is null && added.TryGetValue(PersonKey(first, last), out var manager))
                units[code].ManagerEmployeeId = manager.Id;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Historical roster imported: added={Added}, skipped existing={Skipped}.", added.Count, skipped);
    }

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var directorate = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "KSSIM", ct);
        var deputy = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "BY", ct);
        if (directorate is null || deputy is null)
        {
            logger.LogWarning("Seed: KSSIM/BY yok — müdürlük şeması atlandı.");
            return;
        }

        deputy.Name = "Başkan Yardımcılığı";
        directorate.Name = "Kültür, Sanat ve Sosyal İşler Müdürlüğü";

        var cats = await db.FacilityCategories.ToDictionaryAsync(x => x.Code!, x => x.Id, ct);
        Guid? Cat(string code) => cats.TryGetValue(code, out var id) ? id : null;

        async Task<OrganizationUnit> Ensure(
            string code,
            string name,
            OrganizationUnitType type,
            Guid parentId,
            OrganizationUnitStatus status,
            string? catCode,
            double? lat,
            double? lng)
        {
            var unit = await db.OrganizationUnits.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Code == code, ct);
            if (unit is null)
            {
                unit = new OrganizationUnit
                {
                    Name = name,
                    Code = code,
                    Type = type,
                    Status = status,
                    ParentId = parentId,
                    FacilityCategoryId = catCode is null ? null : Cat(catCode),
                    Latitude = lat,
                    Longitude = lng,
                    CreatedBy = "pdf-seed"
                };
                db.OrganizationUnits.Add(unit);
                await db.SaveChangesAsync(ct);
                return unit;
            }

            unit.IsDeleted = false;
            unit.DeletedAtUtc = null;
            unit.DeletedBy = null;
            unit.Name = name;
            unit.Type = type;
            unit.Status = status;
            unit.ParentId = parentId;
            if (catCode is not null)
                unit.FacilityCategoryId = Cat(catCode);
            if (lat is not null)
            {
                unit.Latitude = lat;
                unit.Longitude = lng;
            }
            unit.UpdatedBy = "pdf-seed";
            return unit;
        }

        var kkm = await Ensure("KKM", "Şehitkamil Kültür ve Kongre Merkezi", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "KULTUR_MERKEZI", 37.0785, 37.3720);
        kkm.Address ??= "Mücahitler Mah. Sezen Aksu Sok., Şehitkamil / Gaziantep";
        var kkmHalls = new (string Code, string Name, int Capacity)[]
        {
            ("KKM_MEHMET_AKIF", "Mehmet Akif Salonu", 1070),
            ("KKM_OAS", "Ömer Asım Aksoy Salonu", 300),
            ("KKM_MUNIF", "Münif Paşa Salonu", 300),
            ("KKM_NURI", "Nuri Paşa Salonu", 300),
            ("KKM_MUTERIM", "Mütercim Asım Salonu", 70),
            ("KKM_SERGI", "Sergi Salonu", 0),
        };
        foreach (var h in kkmHalls)
        {
            var hall = await Ensure(h.Code, h.Name, OrganizationUnitType.Facility, kkm.Id, OrganizationUnitStatus.Active, "KULTUR_MERKEZI", 37.0785, 37.3720);
            hall.Address = kkm.Address;
            if (h.Capacity > 0)
                hall.Capacity = h.Capacity;
        }
        var sanat = await Ensure("SANAT", "Şehitkamil Sanat Merkezi", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "KULTUR_MERKEZI", 37.0912, 37.3515);
        var nikah = await Ensure("NIKAH", "Şehitkamil Nikah Salonu", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "SOSYAL_TESIS", 37.0850, 37.3650);
        var dtss = await Ensure("DTSS", "Devlet Tiyatroları Şehitkamil Sahnesi", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "KULTUR_MERKEZI", 37.0740, 37.3810);
        var sami = await Ensure("SAMI", "M. Sami Benli Kütüphanesi", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "KUTUPHANE", 37.0890, 37.3600);
        await Ensure("SAMI_COCUK", "M. Sami Benli Kütüphanesi Çocuk Kütüphanesi", OrganizationUnitType.SubUnit, sami.Id, OrganizationUnitStatus.Active, "KUTUPHANE", null, null);
        var genc = await Ensure("GENCLIK_KUT", "Gençlik Kütüphaneleri", OrganizationUnitType.SubUnit, directorate.Id, OrganizationUnitStatus.Active, "KUTUPHANE", null, null);
        var bilim = await Ensure("BILIM", "Bilim Şehitkamil", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "GENCLIK_MERKEZI", 37.0955, 37.3400);
        var gezi = await Ensure("GEZI", "Kültürel Geziler", OrganizationUnitType.MainUnit, directorate.Id, OrganizationUnitStatus.Active, null, null, null);
        var agro = await Ensure("AGROPARK", "Agropark", OrganizationUnitType.Facility, directorate.Id, OrganizationUnitStatus.Active, "SOSYAL_TESIS", 37.1860, 37.2860);
        await Ensure("IDARI", "İdari Büro", OrganizationUnitType.SubUnit, dtss.Id, OrganizationUnitStatus.Active, "IDARI_BINA", null, null);

        var youth = new (string Code, string Name, OrganizationUnitStatus Status, double Lat, double Lng)[]
        {
            ("GK_AKTOPRAK", "Aktoprak Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1865, 37.2870),
            ("GK_ALINACAR", "Ali Nacar Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1200, 37.3400),
            ("GK_BASAK", "Başak Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1300, 37.3500),
            ("GK_BELKIS", "Belkıs Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1400, 37.3000),
            ("GK_EYUPSULTAN", "Eyüpsultan Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1100, 37.3700),
            ("GK_KUZEYSEHIR", "Kuzeyşehir Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1500, 37.3300),
            ("GK_MAE", "Mehmet Akif Ersoy Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1000, 37.3450),
            ("GK_MERVE", "Merveşehir Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1250, 37.3550),
            ("GK_MUTERIM", "Mütercim Asım Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1050, 37.3650),
            ("GK_NURTEPE", "Nurtepe Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1150, 37.3250),
            ("GK_OKTAY", "Oktay Yalçın Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1350, 37.3350),
            ("GK_PIRSULTAN", "Pirsultan Gençlik Kütüphanesi", OrganizationUnitStatus.Active, 37.1450, 37.3150),
            ("GK_ZEYTINLI", "Zeytinli ŞESEM", OrganizationUnitStatus.Active, 37.1600, 37.3200),
            ("GK_AKSU", "Abdulkadir Aksu Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1180, 37.3480),
            ("GK_NFK", "Necip Fazıl Kısakürek Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1220, 37.3380),
            ("GK_YESILOVA", "Yeşilova Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1280, 37.3280),
            ("GK_KARACAGOLAN", "Karacaoğlan Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1320, 37.3180),
            ("GK_KOCATEPE", "Kocatepe Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1360, 37.3080),
            ("GK_SIRINEVLER", "Şirinevler Gençlik Kütüphanesi", OrganizationUnitStatus.Closed, 37.1400, 37.2980),
            ("GK_SEYRANTEPE", "Seyrantepe Gençlik Kütüphanesi", OrganizationUnitStatus.UnderRenovation, 37.1480, 37.3420),
            ("GK_ERBAKAN", "Necmettin Erbakan Gençlik Kütüphanesi", OrganizationUnitStatus.UnderRenovation, 37.1520, 37.3520),
        };
        foreach (var y in youth)
            await Ensure(y.Code, y.Name, OrganizationUnitType.Facility, genc.Id, y.Status, "KUTUPHANE", y.Lat, y.Lng);

        await db.SaveChangesAsync(ct);
        await MergeAliasAsync(db, "FAC_KKM", "KKM", ct);
        await MergeAliasAsync(db, "FAC_SANAT", "SANAT", ct);
        await MergeAliasAsync(db, "FAC_NIKAH", "NIKAH", ct);
        await MergeAliasAsync(db, "FAC_BILIM", "BILIM", ct);
        await MergeAliasAsync(db, "FAC_SPOR", "AGROPARK", ct);
        await MergeAliasAsync(db, "FAC_KUT", "GK_MAE", ct);
        await RetireAsync(db, ["DT_TEKNO", "FAC_OKUL", "FAC_EKSIK", "TEKNO_ATOLYE", "FAC_KKM", "FAC_SANAT", "FAC_NIKAH", "FAC_BILIM", "FAC_SPOR", "FAC_KUT"], ct);

        var mehmetAkif = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "KKM_MEHMET_AKIF", ct);
        var sergiHall = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "KKM_SERGI", ct);
        if (mehmetAkif is not null)
        {
            foreach (var ev in await db.Events.Where(e => e.Title.Contains("Dijital Belediyecilik")).ToListAsync(ct))
                ev.FacilityId = mehmetAkif.Id;
            if (!await db.Events.AnyAsync(e => e.FacilityId == mehmetAkif.Id && e.Status == EventStatus.Completed, ct))
            {
                var start = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-12).AddHours(17), DateTimeKind.Utc);
                db.Events.Add(new Event
                {
                    Title = "Oda orkestrası konseri",
                    Description = "Mehmet Akif Salonu — tamamlanan örnek tahsis.",
                    StartAtUtc = start,
                    EndAtUtc = start.AddHours(2),
                    Status = EventStatus.Completed,
                    FacilityId = mehmetAkif.Id,
                    ExpectedAttendees = 900,
                    ActualAttendees = 842,
                    Category = "culture",
                    CreatedBy = "pdf-seed"
                });
            }
        }
        if (sergiHall is not null)
        {
            foreach (var ev in await db.Events.Where(e => e.Title.Contains("Sergital")).ToListAsync(ct))
                ev.FacilityId = sergiHall.Id;
        }
        foreach (var ev in await db.Events.Where(e => e.Status == EventStatus.Completed && e.ActualAttendees == null).ToListAsync(ct))
            ev.ActualAttendees = ev.ExpectedAttendees;
        await db.SaveChangesAsync(ct);

        var titles = await EnsureTitlesAsync(db, ct);
        var duties = await EnsureDutiesAsync(db, ct);
        var sekabel = await db.EmploymentTypes.SingleAsync(x => x.Code == "SEKABEL", ct);
        var memur = await db.EmploymentTypes.SingleAsync(x => x.Code == "MEMUR", ct);
        var units = await db.OrganizationUnits.Where(x => x.Code != null).ToDictionaryAsync(x => x.Code!, ct);

        Guid U(string code) => units[code].Id;
        Guid? F(string? code) => string.IsNullOrEmpty(code) ? null : units[code].Id;

        JobTitle Title(string name) => titles[name];
        JobDuty Duty(string name) => duties[name];

        var people = Roster();
        var existing = await db.Employees.IgnoreQueryFilters().ToListAsync(ct);
        var byKey = existing
            .GroupBy(e => PersonKey(e.FirstName, e.LastName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var nextNo = existing
            .Select(e => e.EmployeeNumber)
            .Where(n => n != null && n.StartsWith("P-", StringComparison.OrdinalIgnoreCase))
            .Select(n => int.TryParse(n![2..], out var v) ? v : 0)
            .DefaultIfEmpty(2000)
            .Max();

        var added = 0;
        var updated = 0;
        foreach (var row in people)
        {
            var key = PersonKey(row.First, row.Last);
            if (!byKey.TryGetValue(key, out var emp))
            {
                nextNo++;
                emp = new Employee
                {
                    FirstName = row.First,
                    LastName = row.Last,
                    EmployeeNumber = $"P-{nextNo}",
                    CreatedBy = "pdf-seed"
                };
                db.Employees.Add(emp);
                byKey[key] = emp;
                added++;
            }
            else
            {
                updated++;
            }

            emp.IsDeleted = false;
            emp.DeletedAtUtc = null;
            emp.Status = EmployeeStatus.Active;
            emp.Gender = GuessGender(row.First);
            emp.UnitId = U(row.Unit);
            emp.FacilityId = F(row.Fac);
            emp.EmploymentTypeId = row.Memur ? memur.Id : sekabel.Id;
            emp.JobTitleId = Title(row.Title).Id;
            emp.UpdatedBy = "pdf-seed";
            if (emp.HireDate is null)
                emp.HireDate = new DateOnly(2023, 1, 1);
            if (emp.ProfileCompletionPercent < 40)
                emp.ProfileCompletionPercent = 55;
        }

        await db.SaveChangesAsync(ct);

        var assignments = await db.EmployeeAssignments.Where(a => a.EndDate == null).ToListAsync(ct);
        foreach (var row in people)
        {
            var emp = byKey[PersonKey(row.First, row.Last)];
            var duty = Duty(row.Duty);
            var current = assignments.FirstOrDefault(a => a.EmployeeId == emp.Id && a.IsPrimary);
            if (current is null)
            {
                db.EmployeeAssignments.Add(new EmployeeAssignment
                {
                    EmployeeId = emp.Id,
                    JobDutyId = duty.Id,
                    IsPrimary = true,
                    StartDate = emp.HireDate ?? new DateOnly(2023, 1, 1),
                    CreatedBy = "pdf-seed"
                });
            }
            else if (current.JobDutyId != duty.Id)
            {
                current.JobDutyId = duty.Id;
                current.UpdatedBy = "pdf-seed";
            }
        }

        await db.SaveChangesAsync(ct);

        void SetManager(string unitCode, string first, string last)
        {
            if (!units.TryGetValue(unitCode, out var unit))
                return;
            if (!byKey.TryGetValue(PersonKey(first, last), out var emp))
                return;
            unit.ManagerEmployeeId = emp.Id;
            unit.UpdatedBy = "pdf-seed";
        }

        SetManager("BY", "Hakan", "Aslansoy");
        SetManager("KSSIM", "Zeynep", "Özbahçivan");
        SetManager("KKM", "Ahmet", "Oral");
        SetManager("SANAT", "Erhan Bozo", "Sarıkaya");
        SetManager("NIKAH", "Tutku", "Yapıcı");
        SetManager("DTSS", "Tarık", "Öndül");
        SetManager("GENCLIK_KUT", "Seydi Vakkas", "Cengiz");
        SetManager("GK_AKTOPRAK", "Harun", "Terlemez");
        SetManager("GK_ALINACAR", "Turgut", "Bozgeyik");
        SetManager("GK_BASAK", "Mahmut", "Orhan");
        SetManager("GK_BELKIS", "Sara", "Akkaya");
        SetManager("GK_EYUPSULTAN", "Erol", "Aras");
        SetManager("GK_KUZEYSEHIR", "Nuriye", "Beyaz");
        SetManager("GK_MAE", "Hasan", "Yılmaz");
        SetManager("GK_MERVE", "Mehmet Emin", "Arslan");
        SetManager("GK_MUTERIM", "Kemal", "Kartal");
        SetManager("GK_NURTEPE", "Fatma", "Ekici");
        SetManager("GK_OKTAY", "Murat", "Kartal");
        SetManager("GK_PIRSULTAN", "Arzu", "Doğru");
        SetManager("GK_ZEYTINLI", "Tuğba", "Çabar");
        SetManager("BILIM", "Eyüp", "Yenikomşu");
        SetManager("AGROPARK", "İrem", "Ölmez");
        await db.SaveChangesAsync(ct);

        foreach (var demo in existing.Where(e => e.EmployeeNumber is "P-1002" or "P-1003" or "P-1004"))
        {
            if (people.Any(p => PersonKey(p.First, p.Last) == PersonKey(demo.FirstName, demo.LastName)))
                continue;
            demo.Status = EmployeeStatus.Passive;
            demo.UpdatedBy = "pdf-seed";
        }

        var idariUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == "idari", ct);
        if (idariUser is not null && byKey.TryGetValue(PersonKey("Tarık", "Öndül"), out var tarik))
        {
            idariUser.EmployeeId = tarik.Id;
            idariUser.DisplayName = "Tarık Öndül";
        }

        var mudurUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == "mudur", ct);
        if (mudurUser is not null && byKey.TryGetValue(PersonKey("Zeynep", "Özbahçivan"), out var zeynep))
            mudurUser.EmployeeId = zeynep.Id;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: müdürlük şeması uygulandı (yeni personel={Added}, güncellenen={Up}).", added, updated);
    }

    private static async Task MergeAliasAsync(AppDbContext db, string fromCode, string toCode, CancellationToken ct)
    {
        var from = await db.OrganizationUnits.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Code == fromCode, ct);
        var to = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == toCode, ct);
        if (from is null || to is null || from.Id == to.Id)
            return;

        foreach (var e in await db.Employees.IgnoreQueryFilters().Where(x => x.FacilityId == from.Id).ToListAsync(ct))
            e.FacilityId = to.Id;
        foreach (var e in await db.Employees.IgnoreQueryFilters().Where(x => x.UnitId == from.Id).ToListAsync(ct))
            e.UnitId = to.Id;
        foreach (var ev in await db.Events.IgnoreQueryFilters().Where(x => x.FacilityId == from.Id).ToListAsync(ct))
            ev.FacilityId = to.Id;

        var fromBals = await db.StockBalances.IgnoreQueryFilters().Where(x => x.LocationId == from.Id).ToListAsync(ct);
        var toBals = await db.StockBalances.IgnoreQueryFilters().Where(x => x.LocationId == to.Id).ToListAsync(ct);
        foreach (var bal in fromBals)
        {
            var dest = toBals.FirstOrDefault(x => x.StockItemId == bal.StockItemId && !x.IsDeleted);
            if (dest is null)
            {
                bal.LocationId = to.Id;
                bal.IsDeleted = false;
            }
            else
            {
                dest.Quantity += bal.Quantity;
                bal.IsDeleted = true;
                bal.DeletedAtUtc = DateTime.UtcNow;
                bal.DeletedBy = "pdf-seed";
            }
        }

        foreach (var mv in await db.StockMovements.IgnoreQueryFilters().Where(x => x.FromLocationId == from.Id).ToListAsync(ct))
            mv.FromLocationId = to.Id;
        foreach (var mv in await db.StockMovements.IgnoreQueryFilters().Where(x => x.ToLocationId == from.Id).ToListAsync(ct))
            mv.ToLocationId = to.Id;

        from.IsDeleted = true;
        from.DeletedAtUtc = DateTime.UtcNow;
        from.DeletedBy = "pdf-seed";
        from.Status = OrganizationUnitStatus.OutOfUse;
        await db.SaveChangesAsync(ct);
    }

    private static async Task RetireAsync(AppDbContext db, IReadOnlyList<string> codes, CancellationToken ct)
    {
        var units = await db.OrganizationUnits.Where(x => x.Code != null && codes.Contains(x.Code)).ToListAsync(ct);
        foreach (var u in units)
        {
            u.Status = OrganizationUnitStatus.OutOfUse;
            u.IsDeleted = true;
            u.DeletedAtUtc = DateTime.UtcNow;
            u.DeletedBy = "pdf-seed";
            u.UpdatedBy = "pdf-seed";
        }
        if (units.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, JobTitle>> EnsureTitlesAsync(AppDbContext db, CancellationToken ct)
    {
        var names = new (string Name, string Code)[]
        {
            ("Başkan Yardımcısı", "BY"),
            ("Müdür", "MUDUR"),
            ("Müdür Yardımcısı", "MY"),
            ("Yönetici", "YONETICI"),
            ("İdari Amir", "IDARI_AMIR"),
            ("Tesis Amiri", "TESIS_AMIR"),
            ("Koordinatör", "KOORD"),
            ("Danışma Personeli", "DANISMA"),
            ("Teknik Personel", "TEKNIK"),
            ("Teknik Personel Şefi", "TEKNIK_SEF"),
            ("Yardımcı Personel", "YARDIMCI"),
            ("Yardımcı Personel Şefi", "YARDIMCI_SEF"),
            ("Eğitmen", "EGITMEN"),
            ("Kütüphane Personeli", "KUTUPHANE"),
            ("Kütüphane Sorumlusu", "KUT_SOR"),
            ("Temizlik Personeli", "TEMIZLIK"),
            ("Nikah Memuru", "NIKAH"),
            ("Memur", "MEMUR_UNVAN"),
            ("Büro Personeli", "BURO"),
            ("Harita Mühendisi", "HARITA"),
            ("Bilim Merkezi Personeli", "BMP"),
            ("Personel", "PERSONEL"),
            ("Çocuk Kütüphanesi Personeli", "COCUK_KUT"),
        };
        var existing = await db.JobTitles.ToListAsync(ct);
        foreach (var (name, code) in names)
        {
            if (existing.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            var t = new JobTitle { Name = name, Code = code, CreatedBy = "pdf-seed" };
            db.JobTitles.Add(t);
            existing.Add(t);
        }
        await db.SaveChangesAsync(ct);
        return existing
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, JobDuty>> EnsureDutiesAsync(AppDbContext db, CancellationToken ct, bool preserveExisting = false)
    {
        var items = new (string Name, DutyCategory Cat)[]
        {
            ("Başkan Yardımcısı", DutyCategory.Manager),
            ("Kültür, Sanat ve Sosyal İşler Müdürü", DutyCategory.Manager),
            ("Kültür, Sanat ve Sosyal İşler Müdür Yardımcısı", DutyCategory.Manager),
            ("Yönetici", DutyCategory.Manager),
            ("İdari Amir", DutyCategory.Manager),
            ("Tesis Amiri", DutyCategory.Manager),
            ("Koordinatör", DutyCategory.Manager),
            ("Bilim Merkezi Yürütücü", DutyCategory.Manager),
            ("Bilim Merkezi Yöneticisi", DutyCategory.Manager),
            ("Danışma Personeli", DutyCategory.Reception),
            ("Danışma Personeli - Salon Tahsis", DutyCategory.Reception),
            ("Danışma Personeli - Gezi Takibi", DutyCategory.Reception),
            ("Teknik Personel", DutyCategory.Technical),
            ("Teknik Personel Şefi", DutyCategory.Technical),
            ("Teknik Personel Şefi - Ses, Işık Sorumlusu", DutyCategory.Technical),
            ("Yardımcı Personel", DutyCategory.Auxiliary),
            ("Yardımcı Personel Şefi", DutyCategory.Auxiliary),
            ("Yardımcı Personel - Gezi Takibi", DutyCategory.Auxiliary),
            ("Gitar Eğitmeni", DutyCategory.Instructor),
            ("Bağlama Eğitmeni", DutyCategory.Instructor),
            ("Piyano Eğitmeni", DutyCategory.Instructor),
            ("Keman Eğitmeni", DutyCategory.Instructor),
            ("Tambur Eğitmeni", DutyCategory.Instructor),
            ("Yağlı Boya Eğitmeni", DutyCategory.Instructor),
            ("Resim Eğitmeni", DutyCategory.Instructor),
            ("Bale Eğitmeni", DutyCategory.Instructor),
            ("Çocuk Gelişimi Eğitmeni", DutyCategory.Instructor),
            ("Matematik Atölyesi Eğitmeni", DutyCategory.Instructor),
            ("Doğa Atölyesi Eğitmeni", DutyCategory.Instructor),
            ("Teknoloji Atölyesi Eğitmeni", DutyCategory.Instructor),
            ("Astronomi Atölyesi Eğitmeni", DutyCategory.Instructor),
            ("Tasarım Atölyesi Eğitmeni", DutyCategory.Instructor),
            ("Kütüphane Personeli", DutyCategory.Library),
            ("Kütüphane Sorumlusu", DutyCategory.Library),
            ("Çocuk Kütüphanesi Personeli", DutyCategory.Library),
            ("Temizlik Personeli", DutyCategory.Cleaning),
            ("Nikah Memuru", DutyCategory.Administrative),
            ("Nikah Defteri Takibi - Kurum Dışı Nikah", DutyCategory.Administrative),
            ("Harita Mühendisi", DutyCategory.Technical),
            ("Büro Personeli", DutyCategory.Administrative),
            ("Memur", DutyCategory.Administrative),
            ("Personel", DutyCategory.Other),
            ("Proje Koordinatörü", DutyCategory.Project),
            ("Destek Programları Koordinatörü", DutyCategory.Project),
            ("Destek Programları Geliştiricisi", DutyCategory.Project),
            ("Proje Geliştiricisi", DutyCategory.Project),
            ("Sosyal Medya Sorumlusu", DutyCategory.SocialMedia),
            ("Halkla İlişkiler", DutyCategory.PublicRelations),
            ("Okulların Program Takibi", DutyCategory.Administrative),
            ("Agropark ve Tarım Müzesi Ziyaretçi Takibi Personeli", DutyCategory.Administrative),
        };
        var existing = await db.JobDuties.ToListAsync(ct);
        foreach (var (name, cat) in items)
        {
            var found = existing.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                found = new JobDuty { Name = name, Category = cat, CreatedBy = "pdf-seed" };
                db.JobDuties.Add(found);
                existing.Add(found);
            }
            else if (!preserveExisting)
            {
                found.Category = cat;
            }
        }
        await db.SaveChangesAsync(ct);
        return existing
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string PersonKey(string first, string last) =>
        $"{first.Trim().ToUpper(Tr)}|{last.Trim().ToUpper(Tr)}";

    private static Gender GuessGender(string first)
    {
        var token = first.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var female = new HashSet<string>(StringComparer.Create(Tr, true))
        {
            "Ayşe", "Zeynep", "Şeyma", "Gülsüm", "Ayça", "Kübra", "Funda", "Betül", "Sudiye", "Arzu",
            "Seda", "Ebru", "Nuran", "Emine", "Gülendam", "Zehra", "Elçin", "Esra", "Münevver", "İrem",
            "Şengül", "Ummut", "Gül", "Güllü", "Aslıhan", "Özlem", "Ceren", "Selma", "Ayşegül", "Tuba",
            "Merve", "İpek", "Gizem", "Medine", "Havva", "Büşra", "Ecem", "Olcay", "Leyla", "Gülçi",
            "Sara", "Adile", "Fatma", "Neslihan", "Nuriye", "Duyğu", "Kezban", "Tuğba", "Burcu", "Tutku",
            "Halime", "Derya", "Bedriye", "Şengül"
        };
        return female.Contains(token) ? Gender.Female : Gender.Male;
    }

    private readonly record struct P(
        string First, string Last, string Unit, string? Fac, string Title, string Duty, bool Memur = false);

    private static P[] Roster() =>
    [
        new("Hakan", "Aslansoy", "BY", null, "Başkan Yardımcısı", "Başkan Yardımcısı"),
        new("Zeynep", "Özbahçivan", "KSSIM", null, "Müdür", "Kültür, Sanat ve Sosyal İşler Müdürü"),
        new("Ayşe", "Uysal", "KSSIM", null, "Müdür Yardımcısı", "Kültür, Sanat ve Sosyal İşler Müdür Yardımcısı"),

        new("Ahmet", "Oral", "KKM", "KKM", "Yönetici", "Yönetici"),
        new("Utku", "Demirsoy", "KKM", "KKM", "Danışma Personeli", "Danışma Personeli - Salon Tahsis"),
        new("Abdullah", "Orhan", "KKM", "KKM", "Teknik Personel Şefi", "Teknik Personel Şefi - Ses, Işık Sorumlusu"),
        new("İbrahim Halil", "Göğüş", "KKM", "KKM", "Yardımcı Personel Şefi", "Yardımcı Personel Şefi"),
        new("Yusuf", "Okumuş", "KKM", "KKM", "Danışma Personeli", "Danışma Personeli"),
        new("Ali", "Döner", "KKM", "KKM", "Teknik Personel", "Teknik Personel"),
        new("Burak", "Özgeren", "KKM", "KKM", "Yardımcı Personel", "Yardımcı Personel"),
        new("Ayşe", "Salıkbağra", "KKM", "KKM", "Danışma Personeli", "Danışma Personeli"),
        new("Mehmet", "Dikyar", "KKM", "KKM", "Teknik Personel", "Teknik Personel"),
        new("İbrahim Halil", "Aslan", "KKM", "KKM", "Yardımcı Personel", "Yardımcı Personel"),
        new("Derya", "Uğur", "KKM", "KKM", "Yardımcı Personel", "Yardımcı Personel"),
        new("İdris", "Beşe", "KKM", "KKM", "Teknik Personel", "Teknik Personel"),
        new("Salih", "Beyoğlu", "KKM", "KKM", "Yardımcı Personel", "Yardımcı Personel"),
        new("Mustafa", "Dertligil", "KKM", "KKM", "Yardımcı Personel", "Yardımcı Personel"),

        new("Erhan Bozo", "Sarıkaya", "SANAT", "SANAT", "İdari Amir", "İdari Amir"),
        new("Şeyma", "Payam", "SANAT", "SANAT", "Danışma Personeli", "Danışma Personeli - Salon Tahsis"),
        new("Adem", "Yalçın", "SANAT", "SANAT", "Teknik Personel Şefi", "Teknik Personel Şefi"),
        new("Halil", "Yegül", "SANAT", "SANAT", "Yardımcı Personel Şefi", "Yardımcı Personel Şefi"),
        new("Ali Osman", "Çuhadar", "SANAT", "SANAT", "Eğitmen", "Gitar Eğitmeni"),
        new("Gülsüm", "Öztürk", "SANAT", "SANAT", "Danışma Personeli", "Danışma Personeli"),
        new("Ali", "Yağcı", "SANAT", "SANAT", "Teknik Personel", "Teknik Personel"),
        new("Ebubekir", "Göksu", "SANAT", "SANAT", "Yardımcı Personel", "Yardımcı Personel"),
        new("Kürşat", "Okyay", "SANAT", "SANAT", "Eğitmen", "Bağlama Eğitmeni"),
        new("M. Haluk", "Aslan", "SANAT", "SANAT", "Kütüphane Sorumlusu", "Kütüphane Sorumlusu"),
        new("Sudiye", "Toprak", "SANAT", "SANAT", "Büro Personeli", "Büro Personeli"),
        new("Ayça", "Tekin", "SANAT", "SANAT", "Danışma Personeli", "Danışma Personeli"),
        new("Ahmet Şerafettin", "Demir", "SANAT", "SANAT", "Teknik Personel", "Teknik Personel"),
        new("Kenan", "Kahraman", "SANAT", "SANAT", "Yardımcı Personel", "Yardımcı Personel"),
        new("Kübra", "Yıldırım", "SANAT", "SANAT", "Eğitmen", "Yağlı Boya Eğitmeni"),
        new("Funda", "Çakmak", "SANAT", "SANAT", "Yardımcı Personel", "Yardımcı Personel"),
        new("Emre", "Polat", "SANAT", "SANAT", "Eğitmen", "Piyano Eğitmeni"),
        new("Betül Zeynep", "Güngör", "SANAT", "SANAT", "Eğitmen", "Keman Eğitmeni"),
        new("Yakup", "Erdoğan", "SANAT", "SANAT", "Eğitmen", "Tambur Eğitmeni"),
        new("Ömer Safa", "Muslu", "SANAT", "SANAT", "Eğitmen", "Gitar Eğitmeni"),
        new("Halime", "Bozlar", "SANAT", "SANAT", "Yardımcı Personel", "Yardımcı Personel"),

        new("Tutku", "Yapıcı", "NIKAH", "NIKAH", "Tesis Amiri", "Tesis Amiri"),
        new("Muhsin", "Çeliker", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Memuru"),
        new("M. Salih", "Çoban", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Memuru"),
        new("Seda", "Akkoç", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Memuru"),
        new("Ebru", "Karaçelebi", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Memuru"),
        new("Nuran", "Kartal", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Memuru"),
        new("Hüseyin", "Yeşil", "NIKAH", "NIKAH", "Harita Mühendisi", "Harita Mühendisi"),
        new("Aziz", "Değirmencioğlu", "NIKAH", "NIKAH", "Personel", "Personel"),
        new("Murat Tahir", "Kimya", "NIKAH", "NIKAH", "Nikah Memuru", "Nikah Defteri Takibi - Kurum Dışı Nikah"),
        new("Melih", "Altunbaş", "NIKAH", "NIKAH", "Personel", "Personel"),
        new("Arzu", "Kılınç", "NIKAH", "NIKAH", "Danışma Personeli", "Danışma Personeli"),
        new("Mehmet", "İçigen", "NIKAH", "NIKAH", "Danışma Personeli", "Danışma Personeli"),
        new("Hüseyin", "Yılmazer", "NIKAH", "NIKAH", "Danışma Personeli", "Danışma Personeli"),
        new("Celal", "Fadıloğlu", "NIKAH", "NIKAH", "Teknik Personel Şefi", "Teknik Personel Şefi"),
        new("Cevdet", "Kalkan", "NIKAH", "NIKAH", "Teknik Personel", "Teknik Personel"),
        new("Cebrail", "Eren", "NIKAH", "NIKAH", "Yardımcı Personel", "Yardımcı Personel"),
        new("Emrah", "Eselik", "NIKAH", "NIKAH", "Yardımcı Personel", "Yardımcı Personel"),
        new("Emine", "Akbayram", "NIKAH", "NIKAH", "Yardımcı Personel", "Yardımcı Personel"),
        new("Gülendam", "Gökşen", "NIKAH", "NIKAH", "Yardımcı Personel", "Yardımcı Personel"),

        new("Tarık", "Öndül", "DTSS", "DTSS", "İdari Amir", "İdari Amir"),
        new("Zehra Merve", "Akyüz", "DTSS", "DTSS", "Danışma Personeli", "Danışma Personeli"),
        new("Elçin", "Özkaynak", "DTSS", "DTSS", "Danışma Personeli", "Danışma Personeli"),
        new("T. Faruk", "Özkesen", "DTSS", "DTSS", "Teknik Personel Şefi", "Teknik Personel Şefi"),
        new("Alper", "Gölgeler", "DTSS", "DTSS", "Teknik Personel", "Teknik Personel"),
        new("Erdal", "Çakmak", "DTSS", "DTSS", "Teknik Personel", "Teknik Personel"),
        new("İsa", "Kaya", "DTSS", "DTSS", "Yardımcı Personel", "Yardımcı Personel"),
        new("Abdulkadir", "Beşe", "DTSS", "DTSS", "Yardımcı Personel", "Yardımcı Personel"),
        new("Samet", "Akbaş", "DTSS", "DTSS", "Yardımcı Personel", "Yardımcı Personel"),
        new("Esra Nur", "Kaya", "DTSS", "DTSS", "Yardımcı Personel", "Yardımcı Personel"),
        new("Münevver", "Korkmaz", "DTSS", "DTSS", "Yardımcı Personel", "Yardımcı Personel"),
        new("Ömer Faruk", "Çevik", "DTSS", "DTSS", "Eğitmen", "Gitar Eğitmeni"),
        new("İrem", "Çimen", "DTSS", "DTSS", "Eğitmen", "Piyano Eğitmeni"),
        new("Hüseyin", "Bozkurt", "DTSS", "DTSS", "Eğitmen", "Resim Eğitmeni"),
        new("Şengül Elçin", "Kurt", "DTSS", "DTSS", "Eğitmen", "Bale Eğitmeni"),
        new("Ummut", "Aydın", "DTSS", "DTSS", "Eğitmen", "Keman Eğitmeni"),
        new("Ozan", "Özer", "IDARI", "DTSS", "Memur", "Memur", true),
        new("Gül", "Kalender", "IDARI", "DTSS", "Büro Personeli", "Büro Personeli"),

        new("Selçuk", "Erdoğan", "SAMI", "SAMI", "Danışma Personeli", "Danışma Personeli"),
        new("Güllü", "Yıldız Çalışkan", "SAMI", "SAMI", "Danışma Personeli", "Danışma Personeli"),
        new("Hakan", "Öztüto", "SAMI", "SAMI", "Danışma Personeli", "Danışma Personeli"),
        new("M. Uğur", "Deniz", "SAMI", "SAMI", "Yardımcı Personel Şefi", "Yardımcı Personel Şefi"),
        new("Mehmet", "Bulut", "SAMI", "SAMI", "Yardımcı Personel", "Yardımcı Personel"),
        new("Mehmet", "Söğüt", "SAMI", "SAMI", "Yardımcı Personel", "Yardımcı Personel"),
        new("Mehmet", "Yılmaz", "SAMI", "SAMI", "Yardımcı Personel", "Yardımcı Personel"),
        new("Bedriye", "Özdemir", "SAMI", "SAMI", "Yardımcı Personel", "Yardımcı Personel"),
        new("Yunus", "Payam", "SAMI", "SAMI", "Teknik Personel", "Teknik Personel"),
        new("Aslıhan", "Berberler", "SAMI_COCUK", "SAMI", "Eğitmen", "Çocuk Gelişimi Eğitmeni"),
        new("Özlem", "Duzcu", "SAMI_COCUK", "SAMI", "Eğitmen", "Çocuk Gelişimi Eğitmeni"),

        new("Eyüp", "Yenikomşu", "BILIM", "BILIM", "Yönetici", "Bilim Merkezi Yürütücü"),
        new("Enes", "Cıkcık", "BILIM", "BILIM", "Yönetici", "Bilim Merkezi Yöneticisi"),
        new("Kadir", "Koçak", "BILIM", "BILIM", "Koordinatör", "Proje Koordinatörü"),
        new("İrem", "Solak", "BILIM", "BILIM", "Koordinatör", "Destek Programları Koordinatörü"),
        new("Ökkeş Yasin", "Yener", "BILIM", "BILIM", "Personel", "Destek Programları Geliştiricisi"),
        new("Esat Efe", "Karalar", "BILIM", "BILIM", "Personel", "Proje Geliştiricisi"),
        new("Ceren", "Sucu", "BILIM", "BILIM", "Eğitmen", "Matematik Atölyesi Eğitmeni"),
        new("Emine", "Yılmaz", "BILIM", "BILIM", "Eğitmen", "Doğa Atölyesi Eğitmeni"),
        new("Selma", "Koçyiğit", "BILIM", "BILIM", "Eğitmen", "Teknoloji Atölyesi Eğitmeni"),
        new("Ayşegül", "Osmanoğlu", "BILIM", "BILIM", "Eğitmen", "Matematik Atölyesi Eğitmeni"),
        new("Tuba", "Arslantaş", "BILIM", "BILIM", "Eğitmen", "Astronomi Atölyesi Eğitmeni"),
        new("M. Merve", "Ercan", "BILIM", "BILIM", "Eğitmen", "Tasarım Atölyesi Eğitmeni"),
        new("İpek Kazaz", "Kaya", "BILIM", "BILIM", "Eğitmen", "Teknoloji Atölyesi Eğitmeni"),
        new("Güllü", "Savaşçı", "BILIM", "BILIM", "Eğitmen", "Astronomi Atölyesi Eğitmeni"),
        new("Gizem", "Melekoğlu", "BILIM", "BILIM", "Personel", "Sosyal Medya Sorumlusu"),
        new("Medine", "Küçükarslan", "BILIM", "BILIM", "Personel", "Halkla İlişkiler"),
        new("Havva", "Şahin", "BILIM", "BILIM", "Danışma Personeli", "Danışma Personeli"),

        new("M. Sinan", "Özen", "GEZI", null, "Danışma Personeli", "Danışma Personeli - Gezi Takibi"),
        new("Olcay", "Sarıkaya", "GEZI", null, "Danışma Personeli", "Danışma Personeli - Gezi Takibi"),
        new("Büşra", "Gökdeniz", "GEZI", null, "Danışma Personeli", "Danışma Personeli - Gezi Takibi"),
        new("Ecem", "Gözübüyük", "GEZI", null, "Danışma Personeli", "Danışma Personeli - Gezi Takibi"),
        new("Abdulkadir", "Güngörmez", "GEZI", null, "Yardımcı Personel", "Yardımcı Personel - Gezi Takibi"),
        new("Batuhan", "Ateş", "GEZI", null, "Yardımcı Personel", "Yardımcı Personel - Gezi Takibi"),
        new("Sedat", "Bozkaya", "GEZI", null, "Danışma Personeli", "Danışma Personeli - Gezi Takibi"),

        new("İrem", "Ölmez", "AGROPARK", "AGROPARK", "Koordinatör", "Okulların Program Takibi"),
        new("Leyla Zümrüt", "Öztürkmen", "AGROPARK", "AGROPARK", "Personel", "Agropark ve Tarım Müzesi Ziyaretçi Takibi Personeli"),
        new("Gülçi", "Yener", "AGROPARK", "AGROPARK", "Personel", "Agropark ve Tarım Müzesi Ziyaretçi Takibi Personeli"),
        new("Muhammed", "Yıldız", "AGROPARK", "AGROPARK", "Personel", "Agropark ve Tarım Müzesi Ziyaretçi Takibi Personeli"),
        new("Zeynep", "Sapçı", "AGROPARK", "AGROPARK", "Personel", "Agropark ve Tarım Müzesi Ziyaretçi Takibi Personeli"),

        new("Seydi Vakkas", "Cengiz", "GENCLIK_KUT", null, "Koordinatör", "Koordinatör"),

        new("Harun", "Terlemez", "GK_AKTOPRAK", "GK_AKTOPRAK", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Yaşar", "Öztürk", "GK_AKTOPRAK", "GK_AKTOPRAK", "Temizlik Personeli", "Temizlik Personeli"),
        new("Vedat", "Baştürk", "GK_AKTOPRAK", "GK_AKTOPRAK", "Temizlik Personeli", "Temizlik Personeli"),

        new("Turgut", "Bozgeyik", "GK_ALINACAR", "GK_ALINACAR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("İrem", "Toksoy", "GK_ALINACAR", "GK_ALINACAR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Şenel", "Bozgeyik", "GK_ALINACAR", "GK_ALINACAR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Merve Nur", "Oğraş", "GK_ALINACAR", "GK_ALINACAR", "Kütüphane Personeli", "Kütüphane Personeli"),

        new("Mahmut", "Orhan", "GK_BASAK", "GK_BASAK", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Adile Kırkız", "Yılmaz", "GK_BASAK", "GK_BASAK", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Burcu", "Gökgöz", "GK_BASAK", "GK_BASAK", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Cuma", "Yılmaz", "GK_BASAK", "GK_BASAK", "Temizlik Personeli", "Temizlik Personeli"),

        new("Sara", "Akkaya", "GK_BELKIS", "GK_BELKIS", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ayşe", "Bağış", "GK_BELKIS", "GK_BELKIS", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mustafa", "Önalan", "GK_BELKIS", "GK_BELKIS", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Yusuf İslam", "Tunç", "GK_BELKIS", "GK_BELKIS", "Kütüphane Personeli", "Kütüphane Personeli", true),
        new("Şerif", "Bağış", "GK_BELKIS", "GK_BELKIS", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Serdar", "Tuna", "GK_BELKIS", "GK_BELKIS", "Temizlik Personeli", "Temizlik Personeli"),

        new("Erol", "Aras", "GK_EYUPSULTAN", "GK_EYUPSULTAN", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Neslihan", "Kömür", "GK_EYUPSULTAN", "GK_EYUPSULTAN", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mehmet Ali", "Yıldız", "GK_EYUPSULTAN", "GK_EYUPSULTAN", "Temizlik Personeli", "Temizlik Personeli"),

        new("Nuriye", "Beyaz", "GK_KUZEYSEHIR", "GK_KUZEYSEHIR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Vakkas", "Satıcı", "GK_KUZEYSEHIR", "GK_KUZEYSEHIR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("İbrahim Halil", "Soytürk", "GK_KUZEYSEHIR", "GK_KUZEYSEHIR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mustafa Teoman", "Söğüt", "GK_KUZEYSEHIR", "GK_KUZEYSEHIR", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mehmet", "Geloğlu", "GK_KUZEYSEHIR", "GK_KUZEYSEHIR", "Temizlik Personeli", "Temizlik Personeli"),

        new("Hasan", "Yılmaz", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Erkan", "Genç", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Duyğu", "Yordam", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mehmet Şükrü", "Abatay", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ebru", "Kaya", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mustafa", "Yaşar", "GK_MAE", "GK_MAE", "Kütüphane Personeli", "Kütüphane Personeli"),

        new("Mehmet Emin", "Arslan", "GK_MERVE", "GK_MERVE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ziya", "Tekin", "GK_MERVE", "GK_MERVE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ali", "Beyazıt", "GK_MERVE", "GK_MERVE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Neslihan", "Taş", "GK_MERVE", "GK_MERVE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ayşe", "Manay", "GK_MERVE", "GK_MERVE", "Temizlik Personeli", "Temizlik Personeli"),
        new("Mehmet", "Dağdelen", "GK_MERVE", "GK_MERVE", "Temizlik Personeli", "Temizlik Personeli"),

        new("Kemal", "Kartal", "GK_MUTERIM", "GK_MUTERIM", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mehmet", "Yağmur", "GK_MUTERIM", "GK_MUTERIM", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Niyazi", "Ekinci", "GK_MUTERIM", "GK_MUTERIM", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Mehmet", "Akın", "GK_MUTERIM", "GK_MUTERIM", "Temizlik Personeli", "Temizlik Personeli"),

        new("Fatma", "Ekici", "GK_NURTEPE", "GK_NURTEPE", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Ayşe", "Şen", "GK_NURTEPE", "GK_NURTEPE", "Kütüphane Personeli", "Kütüphane Personeli"),

        new("Murat", "Kartal", "GK_OKTAY", "GK_OKTAY", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Fatih", "Bay", "GK_OKTAY", "GK_OKTAY", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Eyüp", "Erbilici", "GK_OKTAY", "GK_OKTAY", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Müslüm", "Yıldıztekin", "GK_OKTAY", "GK_OKTAY", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Leyla", "Rüzgar", "GK_OKTAY", "GK_OKTAY", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Kezban", "Tekbaş", "GK_OKTAY", "GK_OKTAY", "Çocuk Kütüphanesi Personeli", "Çocuk Kütüphanesi Personeli"),
        new("Fatma", "Aksoy", "GK_OKTAY", "GK_OKTAY", "Temizlik Personeli", "Temizlik Personeli"),

        new("Arzu", "Doğru", "GK_PIRSULTAN", "GK_PIRSULTAN", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Özlem", "Sağlam", "GK_PIRSULTAN", "GK_PIRSULTAN", "Kütüphane Personeli", "Kütüphane Personeli"),
        new("Fatih", "Gül", "GK_PIRSULTAN", "GK_PIRSULTAN", "Temizlik Personeli", "Temizlik Personeli"),

        new("Tuğba", "Çabar", "GK_ZEYTINLI", "GK_ZEYTINLI", "Personel", "Personel"),
        new("Murat", "İpek", "GK_ZEYTINLI", "GK_ZEYTINLI", "Personel", "Personel"),
        new("Hayri Yaşar", "Özmen", "GK_ZEYTINLI", "GK_ZEYTINLI", "Temizlik Personeli", "Temizlik Personeli"),
    ];
}
