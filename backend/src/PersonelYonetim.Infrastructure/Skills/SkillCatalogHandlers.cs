using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Skills;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Skills;

/// <summary>
/// Yetkinlik kataloğu handler'ları — katalog listesi (istatistikli),
/// detay (personel dağılımı) ve CRUD.
/// </summary>
internal static class SkillCatalogHelpers
{
    public static string ToCategoryLabel(SkillCategory category) => category switch
    {
        SkillCategory.Technical => "Teknik",
        SkillCategory.EducationWorkshop => "Eğitim / Atölye",
        SkillCategory.Administrative => "İdari",
        SkillCategory.Communication => "İletişim",
        SkillCategory.Language => "Dil",
        _ => category.ToString()
    };

    public static string ToLevelLabel(SkillLevel level) => level switch
    {
        SkillLevel.Beginner => "Başlangıç",
        SkillLevel.Intermediate => "Orta",
        SkillLevel.Advanced => "İleri",
        SkillLevel.Expert => "Uzman",
        _ => level.ToString()
    };

    public static IReadOnlyList<SkillLevelCountDto> BuildLevelBreakdown(
        IEnumerable<SkillLevel> levels)
    {
        var counts = levels
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        return Enum.GetValues<SkillLevel>()
            .Select(level => new SkillLevelCountDto
            {
                Label = ToLevelLabel(level),
                Count = counts.GetValueOrDefault(level)
            })
            .ToList();
    }
}

public sealed class GetSkillCatalogHandler : IRequestHandler<GetSkillCatalogQuery, SkillCatalogDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSkillCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SkillCatalogDto> Handle(GetSkillCatalogQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView)
            && !_currentUser.HasPermission(PermissionCodes.SkillsManage))
        {
            throw new ForbiddenException("Yetkinlik kataloğunu görüntüleme yetkiniz yok.");
        }

        var skills = await _db.Skills
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Category,
                x.IsActive,
                Levels = x.EmployeeSkills.Select(es => es.Level).ToList(),
                CertificateCount = x.EmployeeSkills.Count(es => es.HasCertificate)
            })
            .ToListAsync(cancellationToken);

        var employeesWithoutSkills = await _db.Employees
            .AsNoTracking()
            .CountAsync(e => !e.Skills.Any(), cancellationToken);

        var items = skills
            .Select(x => new SkillCatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Category = (int)x.Category,
                CategoryLabel = SkillCatalogHelpers.ToCategoryLabel(x.Category),
                IsActive = x.IsActive,
                EmployeeCount = x.Levels.Count,
                CertificateCount = x.CertificateCount,
                LevelBreakdown = SkillCatalogHelpers.BuildLevelBreakdown(x.Levels)
            })
            .ToList();

        var categories = skills
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .Select(category => new SkillCategoryGroupDto
            {
                Category = (int)category,
                CategoryLabel = SkillCatalogHelpers.ToCategoryLabel(category),
                Skills = items
                    .Where(i => i.Category == (int)category)
                    .OrderBy(i => i.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: true))
                    .ToList()
            })
            .ToList();

        return new SkillCatalogDto
        {
            TotalSkills = items.Count,
            ActiveSkills = items.Count(x => x.IsActive),
            TotalAssignments = items.Sum(x => x.EmployeeCount),
            EmployeesWithoutSkills = employeesWithoutSkills,
            Categories = categories
        };
    }
}

public sealed class GetSkillCatalogDetailHandler
    : IRequestHandler<GetSkillCatalogDetailQuery, SkillCatalogDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSkillCatalogDetailHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SkillCatalogDetailDto?> Handle(
        GetSkillCatalogDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Yetkinlik detayını görüntüleme yetkiniz yok.");

        var skill = await _db.Skills
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new { x.Id, x.Name, x.Category, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (skill is null)
            return null;

        var assignments = await _db.EmployeeSkills
            .AsNoTracking()
            .Where(x => x.SkillId == request.Id)
            .Select(x => new
            {
                EmployeeId = x.Employee.Id,
                x.Employee.FirstName,
                x.Employee.LastName,
                UnitName = x.Employee.Unit != null ? x.Employee.Unit.Name : null,
                JobTitleName = x.Employee.JobTitle != null ? x.Employee.JobTitle.Name : null,
                x.Level,
                x.HasCertificate,
                x.ExperienceDuration
            })
            .ToListAsync(cancellationToken);

        var employees = assignments
            .OrderByDescending(x => x.Level)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(x => new SkillEmployeeItemDto
            {
                Id = x.EmployeeId,
                FullName = $"{x.FirstName} {x.LastName}".Trim(),
                UnitName = x.UnitName,
                JobTitleName = x.JobTitleName,
                LevelLabel = SkillCatalogHelpers.ToLevelLabel(x.Level),
                HasCertificate = x.HasCertificate,
                ExperienceDuration = x.ExperienceDuration
            })
            .ToList();

        return new SkillCatalogDetailDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Category = (int)skill.Category,
            CategoryLabel = SkillCatalogHelpers.ToCategoryLabel(skill.Category),
            IsActive = skill.IsActive,
            EmployeeCount = employees.Count,
            CertificateCount = assignments.Count(x => x.HasCertificate),
            LevelBreakdown = SkillCatalogHelpers.BuildLevelBreakdown(assignments.Select(x => x.Level)),
            Employees = employees
        };
    }
}

public sealed class CreateSkillHandler : IRequestHandler<CreateSkillCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik eklemek için Skills.Manage gerekir.");

        var name = request.Name.Trim();
        var exists = await _db.Skills.AnyAsync(
            x => x.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (exists)
            throw new ConflictException("Bu isimde bir yetkinlik zaten var.");

        var entity = new Skill
        {
            Name = name,
            Category = request.Category,
            IsActive = true,
            CreatedBy = _currentUser.UserName ?? "system"
        };

        _db.Skills.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed class UpdateSkillHandler : IRequestHandler<UpdateSkillCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik güncellemek için Skills.Manage gerekir.");

        var entity = await _db.Skills
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yetkinlik bulunamadı.");

        var name = request.Name.Trim();
        var exists = await _db.Skills.AnyAsync(
            x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (exists)
            throw new ConflictException("Bu isimde bir yetkinlik zaten var.");

        entity.Name = name;
        entity.Category = request.Category;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteSkillHandler : IRequestHandler<DeleteSkillCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik silmek için Skills.Manage gerekir.");

        var entity = await _db.Skills
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yetkinlik bulunamadı.");

        var inUse = await _db.EmployeeSkills.AnyAsync(x => x.SkillId == request.Id, cancellationToken);
        if (inUse)
            throw new ConflictException(
                "Bu yetkinlik personellere atanmış durumda. Silmek yerine pasife alabilirsiniz.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}
