using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Infrastructure.Persistence.Seed;

public static class EmployeeMainUnitsUpdate
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        const string marker = "Organization.EmployeeMainUnits20261027";
        if (await db.AppSettings.AnyAsync(x => x.Key == marker, ct)) return;
        var directorate = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Code == "KSSIM" && x.Type == OrganizationUnitType.Directorate, ct);
        if (directorate is null) return;
        var existing = await db.OrganizationUnits.Where(x => x.ParentId == directorate.Id && x.Type == OrganizationUnitType.MainUnit).ToListAsync(ct);
        foreach (var (code, name) in new[] {
            ("ANA_KULTUR", "Kültür"), ("ANA_SANAT", "Sanat"), ("ANA_BILIM", "Bilim"),
            ("GEZI", "Kültürel Geziler"), ("ANA_KUTUPHANE", "Kütüphane Hizmetleri"),
            ("ANA_DANISMA", "Danışma"), ("ANA_TEMIZLIK", "Temizlik Ekibi")
        })
        {
            if (existing.Any(x => x.Code == code || x.Name == name)) continue;
            db.OrganizationUnits.Add(new OrganizationUnit { Code = code, Name = name, Type = OrganizationUnitType.MainUnit, ParentId = directorate.Id, Status = OrganizationUnitStatus.Active });
        }
        db.AppSettings.Add(new AppSetting { Key = marker, Value = "true", GroupName = "Organizasyon", DisplayName = "Personel ana birimleri tamamlandı", IsReadOnly = true });
        await db.SaveChangesAsync(ct);
    }
}
