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
        await SeedRolePermissionsAsync(db, logger, cancellationToken);
        await SeedEmploymentTypesAsync(db, logger, cancellationToken);
        await SeedFacilityCategoriesAsync(db, logger, cancellationToken);
        await SeedOrganizationAsync(db, logger, cancellationToken);
        await SeedSkillsAsync(db, logger, cancellationToken);
        await SeedCertificateDefinitionsAsync(db, logger, cancellationToken);

        // Örnek personel / demo TCKN / örnek bildirim yalnızca Development
        if (environment.IsDevelopment())
        {
            await SeedSampleEmployeesAsync(db, logger, cancellationToken);
            await SeedDemoSensitiveDataAsync(db, logger, cancellationToken);
            await SeedSampleNotificationsAsync(db, logger, cancellationToken);
        }
        else
        {
            logger.LogInformation("Seed: Production — örnek personel/demo hassas veri atlandı.");
        }

        await SeedAdminUserAsync(db, configuration, environment, logger, cancellationToken);
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

        var existingLinks = await db.RolePermissions
            .Select(x => new { x.RoleId, x.PermissionId })
            .ToListAsync(ct);
        var existingSet = existingLinks
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var toAdd = new List<RolePermission>();

        foreach (var (roleCode, permissionCodes) in RolePermissionMatrix.GetMap())
        {
            if (!roleByCode.TryGetValue(roleCode, out var roleId))
                continue;

            foreach (var permissionCode in permissionCodes)
            {
                if (!permissionByCode.TryGetValue(permissionCode, out var permissionId))
                    continue;

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

        if (toAdd.Count == 0)
            return;

        db.RolePermissions.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} rol-yetki bağlantısı eklendi.", toAdd.Count);
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
        if (await db.FacilityCategories.AnyAsync(ct))
            return;

        var items = new (string Name, string Code, int Order)[]
        {
            ("Kültür Merkezi", "KULTUR_MERKEZI", 1),
            ("Spor Tesisi", "SPOR_TESISI", 2),
            ("Kütüphane", "KUTUPHANE", 3),
            ("Sosyal Tesis", "SOSYAL_TESIS", 4),
            ("Gençlik Merkezi", "GENCLIK_MERKEZI", 5),
            ("Kurs Merkezi", "KURS_MERKEZI", 6),
            ("İdari Bina", "IDARI_BINA", 7),
            ("Diğer", "DIGER", 99)
        };

        db.FacilityCategories.AddRange(items.Select(x => new FacilityCategory
        {
            Name = x.Name,
            Code = x.Code,
            SortOrder = x.Order,
            IsActive = true,
            CreatedBy = "seed"
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: tesis türleri eklendi.");
    }

    private static async Task SeedOrganizationAsync(AppDbContext db, ILogger logger, CancellationToken ct)
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
                password = "ChangeMe!123";
            else
                throw new InvalidOperationException(
                    "Seed:AdminPassword Production ortamında zorunludur (User Secrets veya ortam değişkeni).");
        }

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
}
