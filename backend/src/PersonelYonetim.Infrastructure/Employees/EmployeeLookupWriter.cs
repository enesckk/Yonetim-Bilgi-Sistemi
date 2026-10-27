using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

public static class EmployeeLookupWriter
{
    private static string Clean(string value) => string.Join(" ", value.Normalize().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Key(string value) => Clean(value).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

    public static async Task<Guid?> ResolveTitleAsync(AppDbContext db, Guid? id, string? name, CancellationToken ct)
    {
        if (id.HasValue || string.IsNullOrWhiteSpace(name)) return id;
        var cleaned = Clean(name);
        var title = (await db.JobTitles.ToListAsync(ct)).FirstOrDefault(x => Key(x.Name) == Key(cleaned));
        if (title is null) { title = new JobTitle { Name = cleaned }; db.JobTitles.Add(title); }
        title.IsActive = true;
        return title.Id;
    }

    public static async Task<Guid?> ResolveDutyAsync(AppDbContext db, Guid? id, string? name, CancellationToken ct)
    {
        if (id.HasValue || string.IsNullOrWhiteSpace(name)) return id;
        var cleaned = Clean(name);
        var duty = (await db.JobDuties.ToListAsync(ct)).FirstOrDefault(x => Key(x.Name) == Key(cleaned));
        if (duty is null) { duty = new JobDuty { Name = cleaned, Category = DutyCategory.Other }; db.JobDuties.Add(duty); }
        duty.IsActive = true;
        return duty.Id;
    }
}
