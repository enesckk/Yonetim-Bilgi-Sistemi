using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Security;
using PersonelYonetim.Domain.Settings;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

/// <summary>
/// İlk çalıştırmada temel verileri yükler.
/// Idempotent: kayıt varsa tekrar eklemez.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        await db.Database.MigrateAsync(cancellationToken);

        await SeedPermissionsAsync(db, logger, cancellationToken);
        await SeedRolesAsync(db, logger, cancellationToken);
        // Canlı ortamda yönetim ekranından düzenlenen yetki matrisi yeniden seed edilmez.
        if (environment.IsDevelopment() || !await db.RolePermissions.AnyAsync(cancellationToken))
            await SeedRolePermissionsAsync(db, logger, cancellationToken);
        await SeedEmploymentTypesAsync(db, logger, cancellationToken);
        await SeedFacilityCategoriesAsync(db, logger, cancellationToken);
        await SeedOrganizationAsync(db, environment, logger, cancellationToken);
        await SeedSkillsAsync(db, logger, cancellationToken);
        await SeedCertificateDefinitionsAsync(db, logger, cancellationToken);
        await SettlementSeeder.SeedAsync(db, environment, logger, cancellationToken);
        await StockSeeder.SeedAsync(db, environment, logger, cancellationToken);

        // Örnek personel / demo TCKN / örnek bildirim yalnızca Development
        if (environment.IsDevelopment())
        {
            await SeedSampleEmployeesAsync(db, logger, cancellationToken);
            await SeedDemoSensitiveDataAsync(db, logger, cancellationToken);
            await SeedSampleNotificationsAsync(db, logger, cancellationToken);
            await SeedSampleEventsAndMapDataAsync(db, logger, cancellationToken);
        }
        else
        {
            logger.LogInformation("Seed: Production — örnek personel/demo hassas veri atlandı.");
        }

        await SeedAdminUserAsync(db, configuration, environment, logger, cancellationToken);
        if (environment.IsDevelopment())
        {
            await SeedDirectorUserAsync(db, configuration, environment, logger, cancellationToken);
            await SeedIdariAmirUserAsync(db, configuration, environment, logger, cancellationToken);
            await SeedUnitHeadsAsync(db, logger, cancellationToken);
            await DirectorateOrgChartSeeder.SeedAsync(db, logger, cancellationToken);
            await SeedFacilityOfficerUsersAsync(db, configuration, environment, logger, cancellationToken);
        }
        await SeedAppSettingsAsync(db, logger, cancellationToken);

        // Düz metin TCKN kaldıysa şifrele (eski seed / ilk kurulum)
        var protector = scope.ServiceProvider.GetRequiredService<INationalIdProtector>();
        await MigratePlainNationalIdsAsync(db, protector, logger, cancellationToken);
        if (environment.IsDevelopment())
            await FixInvalidDemoNationalIdAsync(db, protector, logger, cancellationToken);
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var existing = await db.Permissions.IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct);

        var toAdd = PermissionCatalog.All
            .Where(p => !existing.Contains(p.Code))
            .Select(p => new Permission
            {
                Code = p.Code,
                Name = p.Name,
                GroupName = p.GroupName,
                Description = p.Description,
                CreatedBy = "seed"
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        db.Permissions.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} yetki eklendi.", toAdd.Count);
    }

    private static async Task SeedRolesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var existingRoles = await db.Roles.IgnoreQueryFilters().ToListAsync(ct);
        var existingCodes = existingRoles
            .Where(x => x.Code != null)
            .Select(x => x.Code!)
            .ToHashSet(StringComparer.Ordinal);

        var updated = 0;
        foreach (var def in RoleCodes.All)
        {
            var current = existingRoles.FirstOrDefault(x => x.Code == def.Code);
            if (current is null) continue;
            if (current.Name == def.Name && current.Description == def.Description) continue;
            current.Name = def.Name;
            current.Description = def.Description;
            current.UpdatedBy = "seed";
            updated++;
        }

        var toAdd = RoleCodes.All
            .Where(r => !existingCodes.Contains(r.Code))
            .Select(r => new Role
            {
                Name = r.Name,
                Code = r.Code,
                Description = r.Description,
                IsSystemRole = true,
                CreatedBy = "seed"
            })
            .ToList();

        if (toAdd.Count > 0)
            db.Roles.AddRange(toAdd);

        if (toAdd.Count == 0 && updated == 0)
            return;

        await db.SaveChangesAsync(ct);
        if (toAdd.Count > 0)
            logger.LogInformation("Seed: {Count} rol eklendi.", toAdd.Count);
        if (updated > 0)
            logger.LogInformation("Seed: {Count} rol açıklaması güncellendi.", updated);
    }

    private static async Task SeedRolePermissionsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var roles = await db.Roles.ToListAsync(ct);
        var permissions = await db.Permissions.ToListAsync(ct);
        var permissionByCode = permissions.ToDictionary(x => x.Code, x => x.Id);
        var roleByCode = roles.Where(r => r.Code != null).ToDictionary(x => x.Code!, x => x.Id);

        var existingLinks = await db.RolePermissions.ToListAsync(ct);
        var existingSet = existingLinks
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var desired = new HashSet<(Guid RoleId, Guid PermissionId)>();
        var toAdd = new List<RolePermission>();

        foreach (var (roleCode, permissionCodes) in RolePermissionMatrix.GetMap())
        {
            if (!roleByCode.TryGetValue(roleCode, out var roleId))
                continue;

            foreach (var permissionCode in permissionCodes)
            {
                if (!permissionByCode.TryGetValue(permissionCode, out var permissionId))
                    continue;

                desired.Add((roleId, permissionId));
                if (existingSet.Contains((roleId, permissionId)))
                    continue;

                toAdd.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    CreatedBy = "seed"
                });
            }
        }

        var matrixRoleIds = RolePermissionMatrix.GetMap().Keys
            .Where(roleByCode.ContainsKey)
            .Select(c => roleByCode[c])
            .ToHashSet();

        var toRemove = existingLinks
            .Where(x => matrixRoleIds.Contains(x.RoleId) && !desired.Contains((x.RoleId, x.PermissionId)))
            .ToList();

        if (toAdd.Count == 0 && toRemove.Count == 0)
            return;

        if (toAdd.Count > 0)
            db.RolePermissions.AddRange(toAdd);
        if (toRemove.Count > 0)
            db.RolePermissions.RemoveRange(toRemove);
        await db.SaveChangesAsync(ct);
        if (toAdd.Count > 0)
            logger.LogInformation("Seed: {Count} rol-yetki bağlantısı eklendi.", toAdd.Count);
        if (toRemove.Count > 0)
            logger.LogInformation("Seed: {Count} fazla rol-yetki bağlantısı kaldırıldı.", toRemove.Count);
    }

    private static async Task SeedEmploymentTypesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.EmploymentTypes.AnyAsync(ct))
            return;

        var items = new (string Name, string Code, int Order)[]
        {
            ("Memur", "MEMUR", 1),
            ("Şekabel Personeli", "SEKABEL", 2),
            ("Sözleşmeli Personel", "SOZLESMELI", 3),
            ("Geçici Personel", "GECICI", 4),
            ("Diğer Şirket Personeli", "DIGER_SIRKET", 5)
        };

        db.EmploymentTypes.AddRange(items.Select(x => new EmploymentType
        {
            Name = x.Name,
            Code = x.Code,
            SortOrder = x.Order,
            IsActive = true,
            CreatedBy = "seed"
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: istihdam türleri eklendi.");
    }

    private static async Task SeedFacilityCategoriesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var items = new (string Name, string Code, int Order)[]
        {
            ("Kültür Merkezi", "KULTUR_MERKEZI", 1),
            ("Spor Tesisi", "SPOR_TESISI", 2),
            ("Kütüphane", "KUTUPHANE", 3),
            ("Sosyal Tesis", "SOSYAL_TESIS", 4),
            ("Gençlik Merkezi", "GENCLIK_MERKEZI", 5),
            ("Kurs Merkezi", "KURS_MERKEZI", 6),
            ("Okul", "OKUL", 7),
            ("İdari Bina", "IDARI_BINA", 8),
            ("Diğer", "DIGER", 99)
        };

        var existing = await db.FacilityCategories.Select(x => x.Code).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = items.Where(x => !existingSet.Contains(x.Code)).ToArray();
        if (toAdd.Length == 0)
            return;

        db.FacilityCategories.AddRange(toAdd.Select(x => new FacilityCategory
        {
            Name = x.Name,
            Code = x.Code,
            SortOrder = x.Order,
            IsActive = true,
            CreatedBy = "seed"
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} tesis türü eklendi.", toAdd.Length);
    }

    private static async Task SeedOrganizationAsync(AppDbContext db, IHostEnvironment environment, ILogger logger, CancellationToken ct)
    {
        if (await db.OrganizationUnits.AnyAsync(ct))
            return;

        var municipality = NewUnit("Şehitkamil Belediyesi", "BELEDIYE", OrganizationUnitType.Municipality);
        db.OrganizationUnits.Add(municipality);
        await db.SaveChangesAsync(ct);

        var deputy = NewUnit("Başkan Yardımcılığı", "BY", OrganizationUnitType.DeputyPresidency, municipality.Id);
        db.OrganizationUnits.Add(deputy);
        await db.SaveChangesAsync(ct);

        var directorate = NewUnit(
            "Kültür, Sanat ve Sosyal İşler Müdürlüğü",
            "KSSIM",
            OrganizationUnitType.Directorate,
            deputy.Id);
        db.OrganizationUnits.Add(directorate);
        await db.SaveChangesAsync(ct);

        var mainUnits = new (string Name, string Code)[]
        {
            ("Şehitkamil Kültür ve Kongre Merkezi", "KKM"),
            ("Şehitkamil Sanat Merkezi", "SANAT"),
            ("Şehitkamil Nikah Salonu", "NIKAH"),
            ("Devlet Tiyatroları Şehitkamil Sahnesi", "DTSS"),
            ("Gençlik Kütüphaneleri", "GENCLIK_KUT"),
            ("Bilim Şehitkamil", "BILIM"),
            ("Agropark", "AGROPARK"),
            ("Kültürel Geziler", "GEZI"),
            ("İdari Büro", "IDARI")
        };

        var mainUnitEntities = mainUnits
            .Select(x => NewUnit(x.Name, x.Code, OrganizationUnitType.MainUnit, directorate.Id))
            .ToList();

        db.OrganizationUnits.AddRange(mainUnitEntities);
        await db.SaveChangesAsync(ct);

        // Aşağıdaki atölye/tesisler örnek veri; Production'a eklenmez.
        if (!environment.IsDevelopment())
            return;

        var bilim = mainUnitEntities.Single(x => x.Code == "BILIM");
        var techWorkshop = NewUnit(
            "Teknoloji Atölyeleri",
            "TEKNO_ATOLYE",
            OrganizationUnitType.SubUnit,
            bilim.Id);
        db.OrganizationUnits.Add(techWorkshop);
        await db.SaveChangesAsync(ct);

        // Tesis: birimden ayrı da olabilir; örnek olarak DTSS sahnesi tesis olarak da kaydedilir
        var dtss = mainUnitEntities.Single(x => x.Code == "DTSS");
        var facility = NewUnit(
            "Devlet Tiyatroları Teknoloji Atölyeleri",
            "DT_TEKNO",
            OrganizationUnitType.Facility,
            dtss.Id,
            idealStaffCount: 8);
        db.OrganizationUnits.Add(facility);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seed: örnek organizasyon ağacı eklendi.");
    }

    /// <summary>
    /// Development: Şehitkamil haritası / etkinlik modülü için örnek tesis koordinatları ve etkinlikler.
    /// Idempotent — kod veya başlık varsa atlar.
    /// </summary>
    private static async Task SeedSampleEventsAndMapDataAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var directorate = await db.OrganizationUnits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "KSSIM", ct);
        if (directorate is null)
        {
            logger.LogWarning("Seed: KSSIM müdürlüğü yok — etkinlik/harita örnek verisi atlandı.");
            return;
        }

        var categories = await db.FacilityCategories.AsNoTracking().ToListAsync(ct);
        Guid? Cat(string code) => categories.FirstOrDefault(c => c.Code == code)?.Id;

        var parentByCode = await db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, ct);

        Guid Parent(string code) =>
            parentByCode.TryGetValue(code, out var id) ? id : directorate.Id;

        // Örnek tesisler (Şehitkamil içinde gerçekçi koordinatlar)
        var demoFacilities = new (string Name, string Code, string ParentCode, string? CatCode, double? Lat, double? Lng, string Address)[]
        {
            ("Şehitkamil Kültür Merkezi", "FAC_KKM", "KKM", "KULTUR_MERKEZI", 37.0785, 37.3720, "Onatlı Mah., Şehitkamil / Gaziantep"),
            ("Şehitkamil Sanat Merkezi Sahnesi", "FAC_SANAT", "SANAT", "KULTUR_MERKEZI", 37.0912, 37.3515, "Atatürk Mah., Şehitkamil"),
            ("Şehitkamil Nikah Salonu", "FAC_NIKAH", "NIKAH", "SOSYAL_TESIS", 37.0850, 37.3650, "15 Temmuz Mah., Şehitkamil"),
            ("Karataş Gençlik Kütüphanesi", "FAC_KUT", "GENCLIK_KUT", "KUTUPHANE", 37.1120, 37.3280, "Karataş Mah., Şehitkamil"),
            ("Aktoprak Spor Tesisi", "FAC_SPOR", "AGROPARK", "SPOR_TESISI", 37.1860, 37.2860, "Aktoprak, Şehitkamil"),
            ("Bilim Şehitkamil Atölye", "FAC_BILIM", "BILIM", "GENCLIK_MERKEZI", 37.0955, 37.3400, "Alparslan Mah., Şehitkamil"),
            ("İncirli Ortaokulu (örnek)", "FAC_OKUL", "IDARI", "OKUL", 37.1020, 37.3580, "İncirli Mah., Şehitkamil"),
            // Konum eksik testi için bilerek boş
            ("Yeni Sosyal Tesis (konum bekliyor)", "FAC_EKSIK", "IDARI", "SOSYAL_TESIS", null, null, "Adres atanacak"),
        };

        var existingCodes = await db.OrganizationUnits.IgnoreQueryFilters()
            .Where(x => x.Code != null)
            .Select(x => x.Code!)
            .ToListAsync(ct);
        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedFacilities = 0;
        foreach (var f in demoFacilities)
        {
            if (codeSet.Contains(f.Code))
                continue;

            db.OrganizationUnits.Add(new OrganizationUnit
            {
                Name = f.Name,
                Code = f.Code,
                Type = OrganizationUnitType.Facility,
                Status = OrganizationUnitStatus.Active,
                ParentId = Parent(f.ParentCode),
                FacilityCategoryId = f.CatCode is null ? null : Cat(f.CatCode),
                Latitude = f.Lat,
                Longitude = f.Lng,
                Address = f.Address,
                IdealStaffCount = 6,
                CreatedBy = "seed"
            });
            addedFacilities++;
            codeSet.Add(f.Code);
        }

        // Mevcut DT_TEKNO tesisine koordinat ver (yoksa)
        var dtTekno = await db.OrganizationUnits
            .FirstOrDefaultAsync(x => x.Code == "DT_TEKNO", ct);
        if (dtTekno is not null && (dtTekno.Latitude is null || dtTekno.Longitude is null))
        {
            dtTekno.Latitude = 37.0740;
            dtTekno.Longitude = 37.3810;
            dtTekno.Address ??= "Devlet Tiyatroları Sahnesi civarı, Şehitkamil";
            dtTekno.FacilityCategoryId ??= Cat("KULTUR_MERKEZI");
            dtTekno.UpdatedBy = "seed";
            addedFacilities++;
        }

        // Seed’deki ana birim isimli kayıtlar tesis değil; koordinatlı demo tesisler yeterli
        if (addedFacilities > 0)
            await db.SaveChangesAsync(ct);

        // Daha önce eklenmiş demo tesislerin eksik kategorisini tamamla
        var demoCodes = demoFacilities.Select(x => x.Code).Append("DT_TEKNO").ToArray();
        var demoUnits = await db.OrganizationUnits
            .Where(x => x.Code != null && demoCodes.Contains(x.Code))
            .ToListAsync(ct);
        var coordUpdated = 0;
        foreach (var unit in demoUnits)
        {
            var def = demoFacilities.FirstOrDefault(d => d.Code == unit.Code);
            if (def.Code is null) continue;
            var changed = false;
            if (unit.Latitude is null && def.Lat is not null)
            {
                unit.Latitude = def.Lat;
                unit.Longitude = def.Lng;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(unit.Address) && def.Address is not null)
            {
                unit.Address = def.Address;
                changed = true;
            }
            if (unit.FacilityCategoryId is null && def.CatCode is not null)
            {
                unit.FacilityCategoryId = Cat(def.CatCode);
                changed = true;
            }
            if (changed)
            {
                unit.UpdatedBy = "seed";
                coordUpdated++;
            }
        }
        if (coordUpdated > 0)
            await db.SaveChangesAsync(ct);

        if (await db.Events.AnyAsync(ct))
        {
            var renamed = await db.Events
                .Where(x => x.Title.StartsWith("Bu ay:"))
                .ToListAsync(ct);
            foreach (var ev in renamed)
                ev.Title = ev.Title.Replace("Bu ay: ", "", StringComparison.Ordinal);
            if (renamed.Count > 0)
                await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed: harita tesisleri güncellendi (yeni={New}, güncellenen={Up}); etkinlikler zaten var.",
                addedFacilities, coordUpdated);
            return;
        }

        var fac = await db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility && x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x, ct);

        OrganizationUnit? F(string code) => fac.TryGetValue(code, out var u) ? u : null;

        var today = DateTime.UtcNow.Date;

        Event Ev(
            string title,
            string? desc,
            EventStatus status,
            DateTime start,
            DateTime? end,
            OrganizationUnit? facility,
            double? lat = null,
            double? lng = null,
            string? address = null) => new()
        {
            Title = title,
            Description = desc,
            Status = status,
            StartAtUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            EndAtUtc = end is null ? null : DateTime.SpecifyKind(end.Value, DateTimeKind.Utc),
            OrganizingUnitId = directorate.Id,
            FacilityId = facility?.Id,
            Latitude = lat ?? facility?.Latitude,
            Longitude = lng ?? facility?.Longitude,
            Address = address ?? facility?.Address,
            Category = SettlementSeeder.GuessCategory(title),
            CreatedBy = "seed"
        };

        var events = new List<Event>
        {
            Ev(
                "Bugün: Çocuk Tiyatrosu Matinesi",
                "Aileler için ücretsiz matine. Kapı açılışı etkinlikten 30 dk önce.",
                EventStatus.Published,
                today.AddHours(14),
                today.AddHours(16),
                F("FAC_SANAT") ?? F("DT_TEKNO")),
            Ev(
                "Bu hafta: Gençlik Kodlama Atölyesi",
                "11–14 yaş robotik ve kodlama. Kontenjan 24.",
                EventStatus.Published,
                today.AddDays(2).AddHours(10),
                today.AddDays(2).AddHours(13),
                F("FAC_BILIM")),
            Ev(
                "Bu hafta: Açık Hava Konseri",
                "Şehitkamil meydanı civarında ücretsiz konser.",
                EventStatus.Published,
                today.AddDays(4).AddHours(19),
                today.AddDays(4).AddHours(21),
                null,
                37.0845, 37.3565,
                "Şehitkamil Meydanı"),
            Ev(
                "Yarın: Nikah Salonu Bilgilendirme",
                "Randevu ve evrak süreçleri hakkında bilgilendirme.",
                EventStatus.Published,
                today.AddDays(1).AddHours(11),
                today.AddDays(1).AddHours(12),
                F("FAC_NIKAH")),
            Ev(
                "Spor Şenliği — Aktoprak",
                "Mahalle turnuvası ve çocuk oyunları.",
                EventStatus.Published,
                today.AddDays(6).AddHours(9),
                today.AddDays(6).AddHours(17),
                F("FAC_SPOR")),
            Ev(
                "Okul Ziyareti — İncirli",
                "Kültür müdürlüğü okul tanıtım ziyareti.",
                EventStatus.Published,
                today.AddDays(3).AddHours(9),
                today.AddDays(3).AddHours(11),
                F("FAC_OKUL")),
            Ev(
                "Kütüphane Okuma Günü",
                "Karataş Gençlik Kütüphanesi’nde toplu okuma.",
                EventStatus.Draft,
                today.AddDays(8).AddHours(15),
                today.AddDays(8).AddHours(17),
                F("FAC_KUT")),
            Ev(
                "Kültür Merkezi Sergital",
                "Yerel sanatçılar resitali — taslak program.",
                EventStatus.Draft,
                today.AddDays(12).AddHours(20),
                today.AddDays(12).AddHours(22),
                F("FAC_KKM")),
            Ev(
                "Geçen ay: Bilim Fuarı",
                "Tamamlanan örnek etkinlik.",
                EventStatus.Completed,
                today.AddMonths(-1).AddDays(5).AddHours(10),
                today.AddMonths(-1).AddDays(5).AddHours(16),
                F("FAC_BILIM")),
            Ev(
                "İptal: Açık Hava Sineması",
                "Hava şartları nedeniyle iptal edildi.",
                EventStatus.Cancelled,
                today.AddDays(9).AddHours(20),
                today.AddDays(9).AddHours(23),
                F("FAC_SANAT")),
            Ev(
                "Seminer — Dijital Belediyecilik",
                "Personel ve paydaş semineri.",
                EventStatus.Published,
                today.AddDays(10).AddHours(13),
                today.AddDays(10).AddHours(16),
                F("FAC_KKM")),
            Ev(
                "Serbest konum: Mahalle Buluşması",
                "Tesis bağlı değil; doğrudan koordinat ile pin.",
                EventStatus.Published,
                today.AddDays(5).AddHours(18),
                today.AddDays(5).AddHours(20),
                null,
                37.1750, 37.1250,
                "Acaroba Mah. buluşma alanı"),
        };

        db.Events.AddRange(events);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed: harita/etkinlik örnek verisi eklendi (tesis yeni={Fac}, etkinlik={Ev}).",
            addedFacilities, events.Count);
    }

    private static OrganizationUnit NewUnit(
        string name,
        string code,
        OrganizationUnitType type,
        Guid? parentId = null,
        int? idealStaffCount = null)
    {
        return new OrganizationUnit
        {
            Name = name,
            Code = code,
            Type = type,
            Status = OrganizationUnitStatus.Active,
            ParentId = parentId,
            IdealStaffCount = idealStaffCount,
            CreatedBy = "seed"
        };
    }

    private static async Task SeedSkillsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Skills.AnyAsync(ct))
            return;

        var skills = new (string Name, SkillCategory Category)[]
        {
            ("Microsoft Office", SkillCategory.Technical),
            ("Excel", SkillCategory.Technical),
            ("Grafik tasarım", SkillCategory.Technical),
            ("Video düzenleme", SkillCategory.Technical),
            ("Sosyal medya yönetimi", SkillCategory.Technical),
            ("Yazılım geliştirme", SkillCategory.Technical),
            ("Robotik kodlama", SkillCategory.Technical),
            ("Ses sistemi kullanımı", SkillCategory.Technical),
            ("Işık sistemi kullanımı", SkillCategory.Technical),
            ("Çocuklarla çalışma", SkillCategory.EducationWorkshop),
            ("Atölye yönetimi", SkillCategory.EducationWorkshop),
            ("Eğitmenlik", SkillCategory.EducationWorkshop),
            ("Resmi yazışma", SkillCategory.Administrative),
            ("Proje yazımı", SkillCategory.Administrative),
            ("Etkinlik planlama", SkillCategory.Administrative),
            ("Halkla ilişkiler", SkillCategory.Communication),
            ("Sunum yapma", SkillCategory.Communication),
            ("İngilizce", SkillCategory.Language)
        };

        db.Skills.AddRange(skills.Select(x => new Skill
        {
            Name = x.Name,
            Category = x.Category,
            IsActive = true,
            CreatedBy = "seed"
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} yetkinlik eklendi.", skills.Length);
    }

    private static async Task SeedCertificateDefinitionsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.CertificateDefinitions.AnyAsync(ct))
            return;

        var items = new (string Name, string Category)[]
        {
            ("İş Sağlığı ve Güvenliği", "Zorunlu"),
            ("İlk Yardım", "Zorunlu"),
            ("Eğitmenlik Sertifikası", "Mesleki"),
            ("Robotik Kodlama Eğitmenliği", "Mesleki"),
            ("Proje Yönetimi", "Mesleki"),
            ("İngilizce Yeterlilik", "Dil")
        };

        db.CertificateDefinitions.AddRange(items.Select(x => new CertificateDefinition
        {
            Name = x.Name,
            Category = x.Category,
            IsActive = true,
            CreatedBy = "seed"
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} sertifika tanımı eklendi.", items.Length);
    }

    private static async Task SeedSampleEmployeesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(ct))
            return;

        var sekabel = await db.EmploymentTypes.SingleAsync(x => x.Code == "SEKABEL", ct);
        var memur = await db.EmploymentTypes.SingleAsync(x => x.Code == "MEMUR", ct);
        var bilim = await db.OrganizationUnits.SingleAsync(x => x.Code == "BILIM", ct);
        var sanat = await db.OrganizationUnits.SingleAsync(x => x.Code == "SANAT", ct);
        var facility = await db.OrganizationUnits.SingleAsync(x => x.Code == "DT_TEKNO", ct);
        var idari = await db.OrganizationUnits.SingleAsync(x => x.Code == "IDARI", ct);

        var titles = new[]
        {
            new JobTitle { Name = "Bilim Merkezi Personeli", Code = "BMP", CreatedBy = "seed" },
            new JobTitle { Name = "Büro Personeli", Code = "BURO", CreatedBy = "seed" },
            new JobTitle { Name = "Teknik Personel", Code = "TEKNIK", CreatedBy = "seed" },
            new JobTitle { Name = "Danışma Personeli", Code = "DANISMA", CreatedBy = "seed" }
        };
        db.JobTitles.AddRange(titles);

        var duties = new[]
        {
            new JobDuty { Name = "Bilim Merkezi Yöneticisi", Category = DutyCategory.Manager, CreatedBy = "seed" },
            new JobDuty { Name = "Okulların program takibi", Category = DutyCategory.Administrative, CreatedBy = "seed" },
            new JobDuty { Name = "Ses ve ışık sistemleri sorumlusu", Category = DutyCategory.Technical, CreatedBy = "seed" },
            new JobDuty { Name = "Teknoloji Atölyesi Eğitmeni", Category = DutyCategory.Instructor, CreatedBy = "seed" },
            new JobDuty { Name = "Kültürel gezi takibi", Category = DutyCategory.Administrative, CreatedBy = "seed" }
        };
        db.JobDuties.AddRange(duties);
        await db.SaveChangesAsync(ct);

        var titleBmp = titles[0];
        var titleBuro = titles[1];
        var titleTeknik = titles[2];
        var dutyManager = duties[0];
        var dutyProgram = duties[1];
        var dutySes = duties[2];
        var dutyEgitmen = duties[3];

        var enes = new Employee
        {
            FirstName = "Enes",
            LastName = "Cıkcık",
            EmployeeNumber = "P-1001",
            Status = EmployeeStatus.Active,
            Gender = Gender.Male,
            PersonalPhone = "0532 000 00 01",
            CorporateEmail = "enes.cikcik@sehitkamil.bel.tr",
            UnitId = bilim.Id,
            // Tesis, seçilen birimin altındaki tesislerden olmalı (DTSS tesisini Bilim personeline bağlama)
            FacilityId = null,
            EmploymentTypeId = sekabel.Id,
            JobTitleId = titleBmp.Id,
            HireDate = new DateOnly(2025, 3, 1),
            DirectorateStartDate = new DateOnly(2025, 3, 1),
            UnitStartDate = new DateOnly(2025, 3, 1),
            DutyStartDate = new DateOnly(2025, 3, 1),
            ProfileCompletionPercent = 85,
            CreatedBy = "seed"
        };

        var ayse = new Employee
        {
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            EmployeeNumber = "P-1002",
            Status = EmployeeStatus.Active,
            Gender = Gender.Female,
            PersonalPhone = "0532 000 00 02",
            UnitId = idari.Id,
            EmploymentTypeId = memur.Id,
            JobTitleId = titleBuro.Id,
            HireDate = new DateOnly(2021, 6, 15),
            ProfileCompletionPercent = 70,
            CreatedBy = "seed"
        };

        var mehmet = new Employee
        {
            FirstName = "Mehmet",
            LastName = "Kaya",
            EmployeeNumber = "P-1003",
            Status = EmployeeStatus.Active,
            Gender = Gender.Male,
            PersonalPhone = "0532 000 00 03",
            UnitId = sanat.Id,
            FacilityId = sanat.Id,
            EmploymentTypeId = sekabel.Id,
            JobTitleId = titleTeknik.Id,
            HireDate = new DateOnly(2023, 1, 10),
            ProfileCompletionPercent = 60,
            CreatedBy = "seed"
        };

        var zeynep = new Employee
        {
            FirstName = "Zeynep",
            LastName = "Demir",
            EmployeeNumber = "P-1004",
            Status = EmployeeStatus.OnLeave,
            Gender = Gender.Female,
            UnitId = bilim.Id,
            FacilityId = null,
            EmploymentTypeId = sekabel.Id,
            JobTitleId = titleBmp.Id,
            HireDate = new DateOnly(2024, 9, 1),
            ProfileCompletionPercent = 45,
            CreatedBy = "seed"
        };

        db.Employees.AddRange(enes, ayse, mehmet, zeynep);
        await db.SaveChangesAsync(ct);

        db.EmployeeAssignments.AddRange(
            new EmployeeAssignment
            {
                EmployeeId = enes.Id,
                JobDutyId = dutyManager.Id,
                IsPrimary = true,
                StartDate = new DateOnly(2025, 3, 1),
                CreatedBy = "seed"
            },
            new EmployeeAssignment
            {
                EmployeeId = ayse.Id,
                JobDutyId = dutyProgram.Id,
                IsPrimary = true,
                StartDate = new DateOnly(2021, 6, 15),
                CreatedBy = "seed"
            },
            new EmployeeAssignment
            {
                EmployeeId = mehmet.Id,
                JobDutyId = dutySes.Id,
                IsPrimary = true,
                StartDate = new DateOnly(2023, 1, 10),
                CreatedBy = "seed"
            },
            new EmployeeAssignment
            {
                EmployeeId = zeynep.Id,
                JobDutyId = dutyEgitmen.Id,
                IsPrimary = true,
                StartDate = new DateOnly(2024, 9, 1),
                CreatedBy = "seed"
            });

        var software = await db.Skills.SingleAsync(x => x.Name == "Yazılım geliştirme", ct);
        var project = await db.Skills.SingleAsync(x => x.Name == "Proje yazımı", ct);
        var robotic = await db.Skills.SingleAsync(x => x.Name == "Robotik kodlama", ct);

        db.EmployeeSkills.AddRange(
            new EmployeeSkill
            {
                EmployeeId = enes.Id,
                SkillId = software.Id,
                Level = SkillLevel.Advanced,
                CreatedBy = "seed"
            },
            new EmployeeSkill
            {
                EmployeeId = enes.Id,
                SkillId = project.Id,
                Level = SkillLevel.Advanced,
                CreatedBy = "seed"
            },
            new EmployeeSkill
            {
                EmployeeId = enes.Id,
                SkillId = robotic.Id,
                Level = SkillLevel.Advanced,
                CreatedBy = "seed"
            });

        db.EducationRecords.Add(new EducationRecord
        {
            EmployeeId = enes.Id,
            Level = EducationLevel.Bachelor,
            University = "Hasan Kalyoncu Üniversitesi",
            Department = "Yazılım Mühendisliği",
            GraduationYear = 2024,
            CompletionStatus = EducationCompletionStatus.Graduated,
            CreatedBy = "seed"
        });

        // Demo TCKN — algoritmik olarak geçerli örnek (Mernis değil, yalnızca format)
        db.EmployeeSensitiveData.Add(new EmployeeSensitiveData
        {
            EmployeeId = enes.Id,
            NationalIdEncrypted = "10000000146",
            CreatedBy = "seed"
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: örnek personeller eklendi (4 kayıt).");
    }

    /// <summary>
    /// Daha önce seed edilmiş DB'lerde TCKN demo kaydı yoksa ekler (idempotent).
    /// </summary>
    private static async Task SeedDemoSensitiveDataAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var enes = await db.Employees.FirstOrDefaultAsync(x => x.EmployeeNumber == "P-1001", ct);
        if (enes is null)
            return;

        var exists = await db.EmployeeSensitiveData.AnyAsync(x => x.EmployeeId == enes.Id, ct);
        if (exists)
            return;

        db.EmployeeSensitiveData.Add(new EmployeeSensitiveData
        {
            EmployeeId = enes.Id,
            NationalIdEncrypted = "10000000146",
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: demo TCKN kaydı eklendi (P-1001).");
    }

    /// <summary>
    /// Eski seed'deki geçersiz demo TCKN (12345678901) → geçerli örnek.
    /// </summary>
    private static async Task FixInvalidDemoNationalIdAsync(
        AppDbContext db,
        INationalIdProtector protector,
        ILogger logger,
        CancellationToken ct)
    {
        const string invalidDemo = "12345678901";
        const string validDemo = "10000000146";

        var rows = await db.EmployeeSensitiveData
            .Where(x => x.NationalIdEncrypted != null)
            .ToListAsync(ct);

        var fixedCount = 0;
        foreach (var row in rows)
        {
            var plain = protector.Unprotect(row.NationalIdEncrypted);
            if (plain != invalidDemo)
                continue;

            row.NationalIdEncrypted = protector.Protect(validDemo);
            row.NationalIdHash = protector.ComputeLookupHash(validDemo);
            fixedCount++;
        }

        if (fixedCount == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} geçersiz demo TCKN düzeltildi.", fixedCount);
    }

    /// <summary>
    /// Eski düz metin TCKN → Protect + Hash (idempotent).
    /// </summary>
    private static async Task MigratePlainNationalIdsAsync(
        AppDbContext db,
        INationalIdProtector protector,
        ILogger logger,
        CancellationToken ct)
    {
        var rows = await db.EmployeeSensitiveData
            .Where(x => x.NationalIdEncrypted != null)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var row in rows)
        {
            var stored = row.NationalIdEncrypted!;
            if (!protector.LooksLikePlainDigits(stored) && !string.IsNullOrEmpty(row.NationalIdHash))
                continue;

            var plain = protector.Unprotect(stored);
            if (string.IsNullOrWhiteSpace(plain))
                continue;

            if (protector.LooksLikePlainDigits(stored))
                row.NationalIdEncrypted = protector.Protect(plain);

            row.NationalIdHash ??= protector.ComputeLookupHash(plain);
            changed++;
        }

        if (changed == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} TCKN kaydı şifre/hash ile güncellendi.", changed);
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        const string adminUserName = "admin";

        if (await db.Users.AnyAsync(x => x.UserName == adminUserName, ct))
            return;

        var password = configuration["Seed:AdminPassword"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
                password = WellKnownSecrets.DevelopmentPassword;
            else
                throw new InvalidOperationException(
                    "Seed:AdminPassword Production ortamında zorunludur (User Secrets veya ortam değişkeni).");
        }

        RejectProductionDefaultPassword(environment, password);

        var hasher = new PasswordHasher<AppUser>();
        var admin = new AppUser
        {
            UserName = adminUserName,
            Email = "admin@sehitkamil.local",
            DisplayName = "Sistem Yöneticisi",
            IsActive = true,
            CreatedBy = "seed"
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        var adminRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.SystemAdmin, ct);
        db.UserRoles.Add(new UserRole
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Seed: varsayılan admin oluşturuldu (kullanıcı: {User}). İlk girişten sonra şifreyi değiştirin.",
            adminUserName);
    }

    private static async Task SeedDirectorUserAsync(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        const string userName = "mudur";

        if (await db.Users.AnyAsync(x => x.UserName == userName, ct))
            return;

        var password = configuration["Seed:AdminPassword"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
                password = WellKnownSecrets.DevelopmentPassword;
            else
                return;
        }

        RejectProductionDefaultPassword(environment, password);

        var hasher = new PasswordHasher<AppUser>();
        var director = new AppUser
        {
            UserName = userName,
            Email = "mudur@sehitkamil.local",
            DisplayName = "Test Müdür",
            IsActive = true,
            CreatedBy = "seed"
        };
        director.PasswordHash = hasher.HashPassword(director, password);

        db.Users.Add(director);
        await db.SaveChangesAsync(ct);

        var directorRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.Director, ct);
        db.UserRoles.Add(new UserRole
        {
            UserId = director.Id,
            RoleId = directorRole.Id,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Seed: müdür kullanıcısı oluşturuldu (kullanıcı: {User}).",
            userName);
    }

    private static async Task SeedIdariAmirUserAsync(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        const string userName = "idari";

        var password = configuration["Seed:AdminPassword"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
                password = WellKnownSecrets.DevelopmentPassword;
            else if (!await db.Users.AnyAsync(x => x.UserName == userName, ct))
                return;
        }

        RejectProductionDefaultPassword(environment, password);

        var hasher = new PasswordHasher<AppUser>();
        var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == userName, ct);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return;

            user = new AppUser
            {
                UserName = userName,
                Email = "idari@sehitkamil.local",
                DisplayName = "Tarık Öndül",
                IsActive = true,
                CreatedBy = "seed"
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var role = await db.Roles.SingleAsync(x => x.Code == RoleCodes.AdministrativeOfficer, ct);
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                CreatedBy = "seed"
            });
            await db.SaveChangesAsync(ct);

            logger.LogWarning(
                "Seed: idari amir kullanıcısı oluşturuldu (kullanıcı: {User}).",
                userName);
        }

        var idariRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.AdministrativeOfficer, ct);
        var unitRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.UnitManager, ct);
        var links = await db.UserRoles.Where(x => x.UserId == user.Id).ToListAsync(ct);
        if (links.All(x => x.RoleId != idariRole.Id))
        {
            var old = links.FirstOrDefault(x => x.RoleId == unitRole.Id);
            if (old is not null) db.UserRoles.Remove(old);
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = idariRole.Id,
                CreatedBy = "seed"
            });
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Seed: idari amir rolü İdari Amir olarak güncellendi.");
        }

        if (!string.Equals(user.DisplayName, "Tarık Öndül", StringComparison.Ordinal))
        {
            user.DisplayName = "Tarık Öndül";
            await db.SaveChangesAsync(ct);
        }

        if (user.EmployeeId is null)
        {
            var unitEmployee = await db.Employees
                .Where(x => x.EmployeeNumber == "P-1002")
                .Select(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (unitEmployee != Guid.Empty)
            {
                user.EmployeeId = unitEmployee;
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Seed: idari amir personel kaydına bağlandı.");
            }
        }
    }

    private static async Task SeedUnitHeadsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var employees = await db.Employees
            .Where(x => x.EmployeeNumber != null && x.Status == EmployeeStatus.Active)
            .Select(x => new { x.Id, x.EmployeeNumber })
            .ToListAsync(ct);
        var byNumber = employees
            .Where(x => x.EmployeeNumber != null)
            .ToDictionary(x => x.EmployeeNumber!, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var map = new (string UnitCode, string EmployeeNumber)[]
        {
            ("IDARI", "P-1002"),
            ("BILIM", "P-1001"),
            ("SANAT", "P-1003"),
            ("GENCLIK_KUT", "P-1004"),
            ("FAC_KUT", "P-1004"),
        };

        var updated = 0;
        foreach (var (unitCode, employeeNumber) in map)
        {
            if (!byNumber.TryGetValue(employeeNumber, out var employeeId))
                continue;
            var unit = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == unitCode, ct);
            if (unit is null || unit.ManagerEmployeeId == employeeId)
                continue;
            unit.ManagerEmployeeId = employeeId;
            unit.UpdatedBy = "seed";
            updated++;
        }

        if (updated == 0)
            return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} birim/tesise amir atandı.", updated);
    }

    private static async Task SeedFacilityOfficerUsersAsync(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        var password = configuration["Seed:AdminPassword"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
            password = WellKnownSecrets.DevelopmentPassword;

        RejectProductionDefaultPassword(environment, password);

        var hasher = new PasswordHasher<AppUser>();
        var role = await db.Roles.SingleAsync(x => x.Code == RoleCodes.UnitManager, ct);
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        string Key(string first, string last) =>
            $"{first.Trim().ToUpper(tr)}|{last.Trim().ToUpper(tr)}";

        var employees = await db.Employees
            .AsNoTracking()
            .Select(x => new { x.Id, x.FirstName, x.LastName })
            .ToListAsync(ct);
        var byKey = employees
            .GroupBy(x => Key(x.FirstName, x.LastName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var officers = new (string UserName, string First, string Last)[]
        {
            ("ahmet", "Ahmet", "Oral"),
            ("erhan", "Erhan Bozo", "Sarıkaya"),
            ("tutku", "Tutku", "Yapıcı"),
            ("eyup", "Eyüp", "Yenikomşu"),
            ("irem", "İrem", "Ölmez"),
            ("vakkas", "Seydi Vakkas", "Cengiz"),
        };

        var added = 0;
        foreach (var (userName, first, last) in officers)
        {
            if (!byKey.TryGetValue(Key(first, last), out var employeeId))
                continue;

            var display = $"{first} {last}".Trim();
            var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == userName, ct);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = userName,
                    Email = $"{userName}@sehitkamil.local",
                    DisplayName = display,
                    IsActive = true,
                    EmployeeId = employeeId,
                    CreatedBy = "seed"
                };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
                added++;
            }
            else
            {
                user.EmployeeId = employeeId;
                user.DisplayName = display;
                user.IsActive = true;
            }

            var hasRole = await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, ct);
            if (!hasRole)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    CreatedBy = "seed"
                });
            }
        }

        await db.SaveChangesAsync(ct);
        if (added > 0)
            logger.LogWarning("Seed: {Count} tesis amiri hesabı açıldı.", added);
    }

    private static async Task SeedSampleNotificationsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var admin = await db.Users.FirstOrDefaultAsync(x => x.UserName == "admin", ct);
        if (admin is null)
            return;

        if (await db.UserNotifications.AnyAsync(x => x.UserId == admin.Id, ct))
            return;

        db.UserNotifications.AddRange(
            new UserNotification
            {
                UserId = admin.Id,
                Title = "Sisteme hoş geldiniz",
                Body = "Personel Bilgi ve Yönetim Sistemi hazır. Bildirimler, size özel kısa mesajlardır.",
                Severity = NotificationSeverity.Success,
                Category = "System",
                LinkUrl = "/",
                CreatedBy = "seed"
            },
            new UserNotification
            {
                UserId = admin.Id,
                Title = "Veri kalitesini kontrol edin",
                Body = "Eksik birim, telefon veya TCKN kayıtları raporlanabilir. Düzenlemek için Veri Kalitesi ekranına gidin.",
                Severity = NotificationSeverity.Warning,
                Category = "DataQuality",
                LinkUrl = "/data-quality",
                CreatedBy = "seed"
            },
            new UserNotification
            {
                UserId = admin.Id,
                Title = "Varsayılan şifreyi değiştirin",
                Body = "İlk kurulum şifresi güvenlik riskidir. Kullanıcılar ekranından şifrenizi güncelleyin.",
                Severity = NotificationSeverity.Danger,
                Category = "Security",
                LinkUrl = "/users",
                CreatedBy = "seed"
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: örnek bildirimler eklendi.");
    }

    private static async Task SeedAppSettingsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var existing = await db.AppSettings.IgnoreQueryFilters().ToListAsync(ct);
        var byKey = existing.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<AppSetting>();
        var metaUpdated = 0;

        foreach (var d in AppSettingCatalog.All)
        {
            if (!byKey.TryGetValue(d.Key, out var row))
            {
                toAdd.Add(new AppSetting
                {
                    Key = d.Key,
                    Value = d.DefaultValue,
                    ValueType = d.ValueType,
                    GroupName = d.GroupName,
                    DisplayName = d.DisplayName,
                    Description = d.Description,
                    IsReadOnly = d.IsReadOnly,
                    CreatedBy = "seed"
                });
                continue;
            }

            // Değer korunur; yalnızca katalogdaki kullanıcıya dönük metalar senkronlanır.
            var changed = false;
            if (row.GroupName != d.GroupName)
            {
                row.GroupName = d.GroupName;
                changed = true;
            }
            if (row.DisplayName != d.DisplayName)
            {
                row.DisplayName = d.DisplayName;
                changed = true;
            }
            if (row.Description != d.Description)
            {
                row.Description = d.Description;
                changed = true;
            }
            if (row.ValueType != d.ValueType)
            {
                row.ValueType = d.ValueType;
                changed = true;
            }
            if (row.IsReadOnly != d.IsReadOnly)
            {
                row.IsReadOnly = d.IsReadOnly;
                changed = true;
            }
            if (changed) metaUpdated++;
        }

        if (toAdd.Count > 0)
            db.AppSettings.AddRange(toAdd);

        if (toAdd.Count == 0 && metaUpdated == 0)
            return;

        await db.SaveChangesAsync(ct);
        if (toAdd.Count > 0)
            logger.LogInformation("Seed: {Count} uygulama ayarı eklendi.", toAdd.Count);
        if (metaUpdated > 0)
            logger.LogInformation("Seed: {Count} ayar açıklaması güncellendi.", metaUpdated);
    }

    private static void RejectProductionDefaultPassword(IHostEnvironment environment, string? password)
    {
        if (!environment.IsDevelopment() && WellKnownSecrets.IsDevelopmentPassword(password))
        {
            throw new InvalidOperationException(
                "Seed:AdminPassword production'da varsayılan geliştirme şifresi olamaz.");
        }
    }
}
