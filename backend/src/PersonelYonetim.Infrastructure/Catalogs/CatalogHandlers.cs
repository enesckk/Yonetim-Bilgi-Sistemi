using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Catalogs;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Catalogs;

internal static class CatalogHelpers
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string DutyCategoryLabel(DutyCategory category) => category switch
    {
        DutyCategory.Manager => "Yönetim",
        DutyCategory.Administrative => "İdari",
        DutyCategory.Instructor => "Eğitmen",
        DutyCategory.Technical => "Teknik",
        DutyCategory.Reception => "Danışma",
        DutyCategory.Library => "Kütüphane",
        DutyCategory.Auxiliary => "Yardımcı",
        DutyCategory.Cleaning => "Temizlik",
        DutyCategory.Project => "Proje",
        DutyCategory.SocialMedia => "Sosyal medya",
        DutyCategory.PublicRelations => "Halkla ilişkiler",
        DutyCategory.Other => "Diğer",
        _ => category.ToString()
    };

    public static IReadOnlyList<CatalogOptionDto> DutyCategoryOptions() =>
        Enum.GetValues<DutyCategory>()
            .Where(c => (byte)c != 0)
            .OrderBy(c => (byte)c)
            .Select(c => new CatalogOptionDto { Value = (int)c, Label = DutyCategoryLabel(c) })
            .ToList();

    public static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    public static IOrderedEnumerable<T> OrderTr<T>(this IEnumerable<T> source, Func<T, string> key) =>
        source.OrderBy(key, StringComparer.Create(Tr, ignoreCase: true));

    public static void EnsureCanView(ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.EmployeesView)
            && !user.HasPermission(PermissionCodes.OrganizationView)
            && !user.HasPermission(PermissionCodes.CatalogsManage))
            throw new ForbiddenException("Katalogları görüntüleme yetkiniz yok.");
    }

    public static void EnsureCanManage(ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.CatalogsManage))
            throw new ForbiddenException("Katalog yönetimi için Catalogs.Manage gerekir.");
    }
}

public sealed class GetCatalogsOverviewHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCatalogsOverviewQuery, CatalogsOverviewDto>
{
    public async Task<CatalogsOverviewDto> Handle(GetCatalogsOverviewQuery request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanView(currentUser);
        return new CatalogsOverviewDto
        {
            JobDutyCount = await db.JobDuties.CountAsync(ct),
            JobTitleCount = await db.JobTitles.CountAsync(ct),
            EmploymentTypeCount = await db.EmploymentTypes.CountAsync(ct),
            FacilityCategoryCount = await db.FacilityCategories.CountAsync(ct),
            DutyCategories = CatalogHelpers.DutyCategoryOptions()
        };
    }
}

// ——— JobDuty ———

public sealed class GetJobDutyCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetJobDutyCatalogQuery, CatalogListDto>
{
    public async Task<CatalogListDto> Handle(GetJobDutyCatalogQuery request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanView(currentUser);

        var rows = await db.JobDuties.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Category,
                x.IsActive,
                UsageCount = x.Assignments.Count
            })
            .ToListAsync(ct);

        var items = rows
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                IsActive = x.IsActive,
                UsageCount = x.UsageCount,
                Category = (int)x.Category,
                CategoryLabel = CatalogHelpers.DutyCategoryLabel(x.Category)
            })
            .OrderTr(x => x.Name)
            .ToList();

        var groups = items
            .GroupBy(x => x.Category ?? 0)
            .OrderBy(g => g.Key)
            .Select(g => new CatalogGroupDto
            {
                Category = g.Key,
                CategoryLabel = g.First().CategoryLabel ?? "",
                Items = g.OrderTr(i => i.Name).ToList()
            })
            .ToList();

        return new CatalogListDto
        {
            TotalCount = items.Count,
            ActiveCount = items.Count(x => x.IsActive),
            InUseCount = items.Count(x => x.UsageCount > 0),
            Items = items,
            Groups = groups
        };
    }
}

public sealed class CreateJobDutyHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateJobDutyCommand, Guid>
{
    public async Task<Guid> Handle(CreateJobDutyCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var name = request.Name.Trim();
        if (await db.JobDuties.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir fiili görev zaten var.");

        var entity = new JobDuty
        {
            Name = name,
            Category = request.Category,
            IsActive = true,
            CreatedBy = currentUser.UserName ?? "system"
        };
        db.JobDuties.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public sealed class UpdateJobDutyHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateJobDutyCommand>
{
    public async Task Handle(UpdateJobDutyCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.JobDuties.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Fiili görev bulunamadı.");
        var name = request.Name.Trim();
        if (await db.JobDuties.AnyAsync(x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir fiili görev zaten var.");

        entity.Name = name;
        entity.Category = request.Category;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteJobDutyHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteJobDutyCommand>
{
    public async Task Handle(DeleteJobDutyCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.JobDuties.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Fiili görev bulunamadı.");

        if (await db.EmployeeAssignments.AnyAsync(x => x.JobDutyId == request.Id, ct))
            throw new ConflictException("Bu görev personellere atanmış. Silmek yerine pasife alın.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

// ——— JobTitle ———

public sealed class GetJobTitleCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetJobTitleCatalogQuery, CatalogListDto>
{
    public async Task<CatalogListDto> Handle(GetJobTitleCatalogQuery request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanView(currentUser);
        var items = await db.JobTitles.AsNoTracking()
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                IsActive = x.IsActive,
                UsageCount = db.Employees.Count(e => e.JobTitleId == x.Id)
            })
            .ToListAsync(ct);

        items = items.OrderTr(x => x.Name).ToList();
        return new CatalogListDto
        {
            TotalCount = items.Count,
            ActiveCount = items.Count(x => x.IsActive),
            InUseCount = items.Count(x => x.UsageCount > 0),
            Items = items
        };
    }
}

public sealed class CreateJobTitleHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateJobTitleCommand, Guid>
{
    public async Task<Guid> Handle(CreateJobTitleCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var name = request.Name.Trim();
        if (await db.JobTitles.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir unvan zaten var.");

        var entity = new JobTitle
        {
            Name = name,
            Code = CatalogHelpers.NormalizeCode(request.Code),
            IsActive = true,
            CreatedBy = currentUser.UserName ?? "system"
        };
        db.JobTitles.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public sealed class UpdateJobTitleHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateJobTitleCommand>
{
    public async Task Handle(UpdateJobTitleCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.JobTitles.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Unvan bulunamadı.");
        var name = request.Name.Trim();
        if (await db.JobTitles.AnyAsync(x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir unvan zaten var.");

        entity.Name = name;
        entity.Code = CatalogHelpers.NormalizeCode(request.Code);
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteJobTitleHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteJobTitleCommand>
{
    public async Task Handle(DeleteJobTitleCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.JobTitles.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Unvan bulunamadı.");
        if (await db.Employees.AnyAsync(x => x.JobTitleId == request.Id, ct))
            throw new ConflictException("Bu unvan personellere atanmış. Silmek yerine pasife alın.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

// ——— EmploymentType ———

public sealed class GetEmploymentTypeCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetEmploymentTypeCatalogQuery, CatalogListDto>
{
    public async Task<CatalogListDto> Handle(GetEmploymentTypeCatalogQuery request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanView(currentUser);
        var items = await db.EmploymentTypes.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                UsageCount = db.Employees.Count(e => e.EmploymentTypeId == x.Id)
            })
            .ToListAsync(ct);

        return new CatalogListDto
        {
            TotalCount = items.Count,
            ActiveCount = items.Count(x => x.IsActive),
            InUseCount = items.Count(x => x.UsageCount > 0),
            Items = items
        };
    }
}

public sealed class CreateEmploymentTypeHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateEmploymentTypeCommand, Guid>
{
    public async Task<Guid> Handle(CreateEmploymentTypeCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var name = request.Name.Trim();
        if (await db.EmploymentTypes.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir istihdam türü zaten var.");

        var entity = new EmploymentType
        {
            Name = name,
            Code = CatalogHelpers.NormalizeCode(request.Code),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedBy = currentUser.UserName ?? "system"
        };
        db.EmploymentTypes.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public sealed class UpdateEmploymentTypeHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateEmploymentTypeCommand>
{
    public async Task Handle(UpdateEmploymentTypeCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.EmploymentTypes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("İstihdam türü bulunamadı.");
        var name = request.Name.Trim();
        if (await db.EmploymentTypes.AnyAsync(x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir istihdam türü zaten var.");

        entity.Name = name;
        entity.Code = CatalogHelpers.NormalizeCode(request.Code);
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteEmploymentTypeHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteEmploymentTypeCommand>
{
    public async Task Handle(DeleteEmploymentTypeCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.EmploymentTypes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("İstihdam türü bulunamadı.");
        if (await db.Employees.AnyAsync(x => x.EmploymentTypeId == request.Id, ct))
            throw new ConflictException("Bu istihdam türü personellere atanmış. Silmek yerine pasife alın.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

// ——— FacilityCategory ———

public sealed class GetFacilityCategoryCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetFacilityCategoryCatalogQuery, CatalogListDto>
{
    public async Task<CatalogListDto> Handle(GetFacilityCategoryCatalogQuery request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanView(currentUser);
        var items = await db.FacilityCategories.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                UsageCount = db.OrganizationUnits.Count(u => u.FacilityCategoryId == x.Id)
            })
            .ToListAsync(ct);

        return new CatalogListDto
        {
            TotalCount = items.Count,
            ActiveCount = items.Count(x => x.IsActive),
            InUseCount = items.Count(x => x.UsageCount > 0),
            Items = items
        };
    }
}

public sealed class CreateFacilityCategoryHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateFacilityCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateFacilityCategoryCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var name = request.Name.Trim();
        if (await db.FacilityCategories.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir tesis türü zaten var.");

        var entity = new FacilityCategory
        {
            Name = name,
            Code = CatalogHelpers.NormalizeCode(request.Code),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedBy = currentUser.UserName ?? "system"
        };
        db.FacilityCategories.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public sealed class UpdateFacilityCategoryHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateFacilityCategoryCommand>
{
    public async Task Handle(UpdateFacilityCategoryCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.FacilityCategories.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Tesis türü bulunamadı.");
        var name = request.Name.Trim();
        if (await db.FacilityCategories.AnyAsync(x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir tesis türü zaten var.");

        entity.Name = name;
        entity.Code = CatalogHelpers.NormalizeCode(request.Code);
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteFacilityCategoryHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteFacilityCategoryCommand>
{
    public async Task Handle(DeleteFacilityCategoryCommand request, CancellationToken ct)
    {
        CatalogHelpers.EnsureCanManage(currentUser);
        var entity = await db.FacilityCategories.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Tesis türü bulunamadı.");
        if (await db.OrganizationUnits.AnyAsync(x => x.FacilityCategoryId == request.Id, ct))
            throw new ConflictException("Bu tesis türü kullanılıyor. Silmek yerine pasife alın.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}
