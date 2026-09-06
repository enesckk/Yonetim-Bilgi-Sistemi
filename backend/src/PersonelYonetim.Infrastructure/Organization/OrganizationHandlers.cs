using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Application.Features.Organization;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Organization;

public sealed class GetOrganizationTreeHandler
    : IRequestHandler<GetOrganizationTreeQuery, IReadOnlyList<OrganizationUnitNodeDto>>
{
    private readonly AppDbContext _db;

    public GetOrganizationTreeHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrganizationUnitNodeDto>> Handle(
        GetOrganizationTreeQuery request,
        CancellationToken cancellationToken)
    {
        var units = await _db.OrganizationUnits
            .AsNoTracking()
            .Include(x => x.ManagerEmployee)
            .Include(x => x.FacilityCategory)
            .Include(x => x.Parent)
            .ToListAsync(cancellationToken);

        var activeEmployees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.UnitId,
                e.FacilityId,
                JobTitleName = e.JobTitle != null ? e.JobTitle.Name : null,
                EmploymentTypeName = e.EmploymentType != null ? e.EmploymentType.Name : null,
                DutyName = e.Assignments
                    .Where(a => a.EndDate == null)
                    .OrderByDescending(a => a.IsPrimary)
                    .Select(a => a.JobDuty.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var dutyByEmployeeId = activeEmployees
            .Where(e => !string.IsNullOrWhiteSpace(e.DutyName))
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First().DutyName!);

        var byParent = units
            .GroupBy(x => x.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        var parentById = units.ToDictionary(u => u.Id, u => u.ParentId);

        List<OrganizationUnitNodeDto> BuildChildren(Guid? parentId)
        {
            var key = parentId ?? Guid.Empty;
            if (!byParent.TryGetValue(key, out var children))
                return [];

            return children
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .Select(x =>
                {
                    // Şema: kişi kendi biriminde görünür. Başka birimin tesisinde
                    // çalışsa bile o tesis kartında listelenmez.
                    var matched = activeEmployees
                        .Where(e => OrgChartPlacement.BelongsTo(x, e.UnitId, e.FacilityId, parentById))
                        .GroupBy(e => e.Id)
                        .Select(g => g.First())
                        .ToList();

                    var staffing = OrgStaffing.ForUnit(x, matched.Select(e =>
                        (e.Id, e.UnitId, e.FacilityId, e.DutyName)), parentById);

                    string? managerDuty = null;
                    if (x.ManagerEmployeeId is Guid managerId)
                        dutyByEmployeeId.TryGetValue(managerId, out managerDuty);

                    var chartPeople = matched
                        .Where(e => x.ManagerEmployeeId == null || e.Id != x.ManagerEmployeeId.Value)
                        .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                        .Take(80)
                        .Select(e => new OrganizationChartPersonDto
                        {
                            Id = e.Id,
                            FullName = $"{e.FirstName} {e.LastName}".Trim(),
                            JobTitleName = e.JobTitleName,
                            DutyName = e.DutyName,
                            EmploymentTypeName = e.EmploymentTypeName
                        })
                        .ToList();

                    return new OrganizationUnitNodeDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Code = x.Code,
                        Type = x.Type,
                        TypeLabel = OrgLabels.Type(x.Type),
                        Status = x.Status,
                        StatusLabel = OrgLabels.Status(x.Status),
                        ParentId = x.ParentId,
                        ParentName = x.Parent?.Name,
                        Description = x.Description,
                        Phone = x.Phone,
                        Email = x.Email,
                        FacilityCategoryId = x.FacilityCategoryId,
                        FacilityCategoryName = x.FacilityCategory?.Name,
                        Address = x.Address,
                        Latitude = x.Latitude,
                        Longitude = x.Longitude,
                        Capacity = x.Capacity,
                        WorkingHours = x.WorkingHours,
                        ManagerEmployeeId = x.ManagerEmployeeId,
                        ManagerName = x.ManagerEmployee is null
                            ? null
                            : $"{x.ManagerEmployee.FirstName} {x.ManagerEmployee.LastName}".Trim(),
                        ManagerDutyName = managerDuty,
                        IdealStaffCount = x.IdealStaffCount,
                        ActiveEmployeeCount = staffing.ActiveCount,
                        MissingStaffCount = staffing.MissingCount,
                        OpenedOn = x.OpenedOn,
                        ClosedOn = x.ClosedOn,
                        UpdatedAtUtc = x.UpdatedAtUtc,
                        DutyBreakdown = staffing.DutyBreakdown,
                        ChartPersonnel = chartPeople,
                        Children = BuildChildren(x.Id)
                    };
                })
                .ToList();
        }

        return BuildChildren(null);
    }
}

public sealed class GetOrganizationUnitDetailHandler
    : IRequestHandler<GetOrganizationUnitDetailQuery, OrganizationUnitDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetOrganizationUnitDetailHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OrganizationUnitDetailDto?> Handle(
        GetOrganizationUnitDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.OrganizationView))
            throw new ForbiddenException("Organizasyon görüntülemek için Organization.View gerekir.");

        var unit = await _db.OrganizationUnits
            .AsNoTracking()
            .Include(x => x.ManagerEmployee)
            .Include(x => x.FacilityCategory)
            .Include(x => x.Parent)
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (unit is null)
            return null;

        var canViewEmployees = _currentUser.HasPermission(PermissionCodes.EmployeesView);
        var canViewMovements = _currentUser.HasPermission(PermissionCodes.MovementsView);

        var parentById = await _db.OrganizationUnits.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.ParentId, cancellationToken);

        var employees = canViewEmployees
            ? await _db.Employees
                .AsNoTracking()
                .Include(e => e.JobTitle)
                .Include(e => e.EmploymentType)
                .Include(e => e.Assignments).ThenInclude(a => a.JobDuty)
                .Include(e => e.Skills).ThenInclude(s => s.Skill)
                .Where(e => e.UnitId == unit.Id || e.FacilityId == unit.Id)
                .ToListAsync(cancellationToken)
            : [];

        employees = employees
            .Where(e => OrgChartPlacement.BelongsTo(unit, e.UnitId, e.FacilityId, parentById))
            .ToList();

        var active = employees.Where(e => e.Status == EmployeeStatus.Active).ToList();
        var staffing = OrgStaffing.ForUnit(
            unit,
            active.Select(e =>
            {
                var duty = e.Assignments
                    .Where(a => a.IsPrimary && a.EndDate == null)
                    .Select(a => a.JobDuty?.Name)
                    .FirstOrDefault();
                return (e.Id, e.UnitId, e.FacilityId, duty);
            }),
            parentById);

        var dutyDist = active
            .Select(e => e.Assignments
                .Where(a => a.IsPrimary && a.EndDate == null)
                .Select(a => a.JobDuty?.Name)
                .FirstOrDefault() ?? "Görevsiz")
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .Select(g => new NamedCountDto { Name = g.Key, Count = g.Count() })
            .ToList();

        var employmentDist = active
            .GroupBy(e => e.EmploymentType?.Name ?? "Belirtilmemiş")
            .OrderByDescending(g => g.Count())
            .Select(g => new NamedCountDto { Name = g.Key, Count = g.Count() })
            .ToList();

        var skills = active
            .SelectMany(e => e.Skills.Select(s => s.Skill.Name))
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(20)
            .Select(g => new NamedCountDto { Name = g.Key, Count = g.Count() })
            .ToList();

        var personnelByDuty = canViewEmployees
            ? active
                .GroupBy(e => e.Assignments
                    .Where(a => a.IsPrimary && a.EndDate == null)
                    .Select(a => a.JobDuty?.Name)
                    .FirstOrDefault() ?? "Görevsiz")
                .OrderBy(g => g.Key)
                .Select(g => new OrganizationPersonnelGroupDto
                {
                    DutyName = g.Key,
                    Employees = g
                        .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                        .Select(e => new OrganizationPersonnelItemDto
                        {
                            Id = e.Id,
                            FullName = e.FullName,
                            EmployeeNumber = e.EmployeeNumber,
                            JobTitleName = e.JobTitle?.Name,
                            StatusLabel = OrgLabels.EmployeeStatus(e.Status),
                            IsPrimaryDuty = true
                        })
                        .ToList()
                })
                .ToList()
            : [];

        var children = unit.Children
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToList();

        IReadOnlyList<OrganizationChildSummaryDto> childSummaries = [];
        if (children.Count > 0)
        {
            var childIds = children.Select(c => c.Id).ToList();
            var childCounts = await _db.Employees
                .AsNoTracking()
                .Where(e => e.Status == EmployeeStatus.Active
                    && ((e.UnitId != null && childIds.Contains(e.UnitId.Value))
                        || (e.FacilityId != null && childIds.Contains(e.FacilityId.Value))))
                .Select(e => new { e.Id, e.UnitId, e.FacilityId })
                .ToListAsync(cancellationToken);

            childSummaries = children
                .Select(c =>
                {
                    var ids = childCounts
                        .Where(e => e.UnitId == c.Id || e.FacilityId == c.Id)
                        .Select(e => e.Id)
                        .Distinct()
                        .Count();
                    return new OrganizationChildSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        TypeLabel = OrgLabels.Type(c.Type),
                        StatusLabel = OrgLabels.Status(c.Status),
                        ActiveEmployeeCount = ids,
                        IdealStaffCount = c.IdealStaffCount
                    };
                })
                .ToList();
        }

        IReadOnlyList<OrganizationMovementSummaryDto> movements = [];
        if (canViewMovements)
        {
            movements = await _db.EmployeeMovements
                .AsNoTracking()
                .Include(m => m.Employee)
                .Where(m =>
                    m.OldUnitId == unit.Id || m.NewUnitId == unit.Id
                    || m.OldFacilityId == unit.Id || m.NewFacilityId == unit.Id)
                .OrderByDescending(m => m.StartDate)
                .Take(12)
                .Select(m => new OrganizationMovementSummaryDto
                {
                    Id = m.Id,
                    MovementTypeLabel = OrgLabels.Movement(m.MovementType),
                    EmployeeName = m.Employee.FullName,
                    StartDate = m.StartDate,
                    Reason = m.Reason
                })
                .ToListAsync(cancellationToken);
        }

        return new OrganizationUnitDetailDto
        {
            Id = unit.Id,
            Name = unit.Name,
            Code = unit.Code,
            Type = unit.Type,
            TypeLabel = OrgLabels.Type(unit.Type),
            Status = unit.Status,
            StatusLabel = OrgLabels.Status(unit.Status),
            ParentId = unit.ParentId,
            ParentName = unit.Parent?.Name,
            Description = unit.Description,
            Phone = unit.Phone,
            Email = unit.Email,
            FacilityCategoryId = unit.FacilityCategoryId,
            FacilityCategoryName = unit.FacilityCategory?.Name,
            Address = unit.Address,
            Latitude = unit.Latitude,
            Longitude = unit.Longitude,
            Capacity = unit.Capacity,
            WorkingHours = unit.WorkingHours,
            ManagerEmployeeId = unit.ManagerEmployeeId,
            ManagerName = unit.ManagerEmployee is null
                ? null
                : $"{unit.ManagerEmployee.FirstName} {unit.ManagerEmployee.LastName}".Trim(),
            IdealStaffCount = unit.IdealStaffCount,
            ActiveEmployeeCount = staffing.ActiveCount,
            MissingStaffCount = staffing.MissingCount,
            OpenedOn = unit.OpenedOn,
            ClosedOn = unit.ClosedOn,
            UpdatedAtUtc = unit.UpdatedAtUtc,
            DutyDistribution = dutyDist,
            EmploymentTypeDistribution = employmentDist,
            Skills = skills,
            ChildFacilities = childSummaries,
            PersonnelByDuty = personnelByDuty,
            RecentMovements = movements
        };
    }
}

public sealed class GetOrganizationFormOptionsHandler
    : IRequestHandler<GetOrganizationFormOptionsQuery, OrganizationFormOptionsDto>
{
    private readonly AppDbContext _db;

    public GetOrganizationFormOptionsHandler(AppDbContext db) => _db = db;

    public async Task<OrganizationFormOptionsDto> Handle(
        GetOrganizationFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var parents = await _db.OrganizationUnits
            .AsNoTracking()
            .Where(x => x.Type != OrganizationUnitType.Facility)
            .OrderBy(x => x.Name)
            .Select(x => new LookupOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Type = x.Type
            })
            .ToListAsync(cancellationToken);

        var managers = await _db.Employees
            .AsNoTracking()
            .Where(x => x.Status == EmployeeStatus.Active)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new LookupOptionDto
            {
                Id = x.Id,
                Name = (x.FirstName + " " + x.LastName).Trim(),
                Code = x.EmployeeNumber
            })
            .ToListAsync(cancellationToken);

        var facilityCategories = await _db.FacilityCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);

        return new OrganizationFormOptionsDto
        {
            Types =
            [
                new EnumOptionDto { Value = (int)OrganizationUnitType.Municipality, Label = "Belediye" },
                new EnumOptionDto { Value = (int)OrganizationUnitType.DeputyPresidency, Label = "Başkan Yardımcılığı" },
                new EnumOptionDto { Value = (int)OrganizationUnitType.Directorate, Label = "Müdürlük" },
                new EnumOptionDto { Value = (int)OrganizationUnitType.MainUnit, Label = "Ana birim" },
                new EnumOptionDto { Value = (int)OrganizationUnitType.SubUnit, Label = "Alt birim" },
                new EnumOptionDto { Value = (int)OrganizationUnitType.Facility, Label = "Tesis" }
            ],
            Statuses =
            [
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.Active, Label = "Aktif" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.Passive, Label = "Pasif" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.Closed, Label = "Kapalı" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.UnderRenovation, Label = "Tadilatta" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.TemporarilyClosed, Label = "Geçici kapalı" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.Planning, Label = "Planlama aşamasında" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.Transferred, Label = "Devredildi" },
                new EnumOptionDto { Value = (int)OrganizationUnitStatus.OutOfUse, Label = "Kullanım dışı" }
            ],
            ParentCandidates = parents,
            Managers = managers,
            FacilityCategories = facilityCategories
        };
    }
}

public sealed class CreateOrganizationUnitHandler : IRequestHandler<CreateOrganizationUnitCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateOrganizationUnitHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateOrganizationUnitCommand request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await OrgWriteRules.EnsureParentRulesAsync(_db, request.ParentId, request.Type, excludeId: null, cancellationToken);
        await OrgWriteRules.EnsureCodeUniqueAsync(_db, request.Code, excludeId: null, cancellationToken);
        await OrgWriteRules.EnsureManagerExistsAsync(_db, request.ManagerEmployeeId, cancellationToken);
        await OrgWriteRules.EnsureFacilityCategoryExistsAsync(
            _db, request.Type, request.FacilityCategoryId, cancellationToken);

        var entity = new OrganizationUnit
        {
            Name = request.Name.Trim(),
            Code = OrgWriteRules.Normalize(request.Code),
            Type = request.Type,
            Status = request.Status,
            ParentId = request.Type == OrganizationUnitType.Municipality ? null : request.ParentId,
            Description = OrgWriteRules.Normalize(request.Description),
            Phone = OrgWriteRules.Normalize(request.Phone),
            Email = OrgWriteRules.Normalize(request.Email),
            FacilityCategoryId = request.Type == OrganizationUnitType.Facility ? request.FacilityCategoryId : null,
            Address = request.Type == OrganizationUnitType.Facility ? OrgWriteRules.Normalize(request.Address) : null,
            Latitude = request.Type == OrganizationUnitType.Facility ? request.Latitude : null,
            Longitude = request.Type == OrganizationUnitType.Facility ? request.Longitude : null,
            Capacity = request.Type == OrganizationUnitType.Facility ? request.Capacity : null,
            WorkingHours = request.Type == OrganizationUnitType.Facility ? OrgWriteRules.Normalize(request.WorkingHours) : null,
            IdealStaffCount = request.IdealStaffCount,
            ManagerEmployeeId = request.ManagerEmployeeId,
            OpenedOn = request.OpenedOn,
            ClosedOn = request.ClosedOn,
            CreatedBy = _currentUser.UserName ?? "system"
        };

        _db.OrganizationUnits.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.OrganizationManage))
            throw new ForbiddenException("Organizasyon yönetmek için Organization.Manage gerekir.");
    }
}

public sealed class UpdateOrganizationUnitHandler : IRequestHandler<UpdateOrganizationUnitCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateOrganizationUnitHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateOrganizationUnitCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.OrganizationManage))
            throw new ForbiddenException("Organizasyon yönetmek için Organization.Manage gerekir.");

        var entity = await _db.OrganizationUnits
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Birim bulunamadı.");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId == request.Id)
                throw new AppValidationException("parentId", "Birim kendi üstü olamaz.");

            var isDescendant = await OrgWriteRules.IsDescendantAsync(_db, request.Id, request.ParentId.Value, cancellationToken);
            if (isDescendant)
                throw new AppValidationException("parentId", "Üst birim, seçilen birimin altından olamaz (döngü).");
        }

        await OrgWriteRules.EnsureParentRulesAsync(_db, request.ParentId, request.Type, request.Id, cancellationToken);
        await OrgWriteRules.EnsureCodeUniqueAsync(_db, request.Code, request.Id, cancellationToken);
        await OrgWriteRules.EnsureManagerExistsAsync(_db, request.ManagerEmployeeId, cancellationToken);
        await OrgWriteRules.EnsureFacilityCategoryExistsAsync(
            _db, request.Type, request.FacilityCategoryId, cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Code = OrgWriteRules.Normalize(request.Code);
        entity.Type = request.Type;
        entity.Status = request.Status;
        entity.ParentId = request.Type == OrganizationUnitType.Municipality ? null : request.ParentId;
        entity.Description = OrgWriteRules.Normalize(request.Description);
        entity.Phone = OrgWriteRules.Normalize(request.Phone);
        entity.Email = OrgWriteRules.Normalize(request.Email);
        entity.FacilityCategoryId = request.Type == OrganizationUnitType.Facility ? request.FacilityCategoryId : null;
        entity.Address = request.Type == OrganizationUnitType.Facility ? OrgWriteRules.Normalize(request.Address) : null;
        entity.Latitude = request.Type == OrganizationUnitType.Facility ? request.Latitude : null;
        entity.Longitude = request.Type == OrganizationUnitType.Facility ? request.Longitude : null;
        entity.Capacity = request.Type == OrganizationUnitType.Facility ? request.Capacity : null;
        entity.WorkingHours = request.Type == OrganizationUnitType.Facility ? OrgWriteRules.Normalize(request.WorkingHours) : null;
        entity.IdealStaffCount = request.IdealStaffCount;
        entity.ManagerEmployeeId = request.ManagerEmployeeId;
        entity.OpenedOn = request.OpenedOn;
        entity.ClosedOn = request.ClosedOn;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteOrganizationUnitHandler : IRequestHandler<DeleteOrganizationUnitCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteOrganizationUnitHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteOrganizationUnitCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.OrganizationManage))
            throw new ForbiddenException("Organizasyon yönetmek için Organization.Manage gerekir.");

        var entity = await _db.OrganizationUnits
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Birim bulunamadı.");

        if (entity.Children.Any(c => !c.IsDeleted))
            throw new ConflictException("Alt birimleri olan kayıt silinemez. Önce alt birimleri taşıyın veya silin.");

        var hasEmployees = await _db.Employees.AnyAsync(
            x => x.UnitId == request.Id || x.FacilityId == request.Id,
            cancellationToken);
        if (hasEmployees)
            throw new ConflictException("Personeli olan birim/tesis silinemez.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Organizasyon şemasında personelin hangi düğümde görüneceği.
/// Tesis alanı yalnızca çalışma yeri olabilir; kadro birimi başkaysa şemada o tesiste listelenmez.
/// </summary>
internal static class OrgChartPlacement
{
    public static bool BelongsTo(
        OrganizationUnit node,
        Guid? employeeUnitId,
        Guid? employeeFacilityId,
        IReadOnlyDictionary<Guid, Guid?> parentById)
    {
        if (node.Type == OrganizationUnitType.Facility)
        {
            if (employeeFacilityId != node.Id || employeeUnitId is not Guid unitId)
                return false;

            return IsSelfOrAncestor(node.Id, unitId, parentById);
        }

        return employeeUnitId == node.Id;
    }

    /// <summary>fromId düğümünden köke giderken targetId görülür mü (kendisi dahil).</summary>
    public static bool IsSelfOrAncestor(
        Guid fromId,
        Guid targetId,
        IReadOnlyDictionary<Guid, Guid?> parentById)
    {
        Guid? current = fromId;
        for (var i = 0; i < 64 && current is Guid id; i++)
        {
            if (id == targetId)
                return true;
            if (!parentById.TryGetValue(id, out var parent))
                break;
            current = parent;
        }

        return false;
    }
}

internal static class OrgStaffing
{
    public sealed record Result(int ActiveCount, int MissingCount, IReadOnlyList<NamedCountDto> DutyBreakdown);

    public static Result ForUnit(
        OrganizationUnit unit,
        IEnumerable<(Guid Id, Guid? UnitId, Guid? FacilityId, string? DutyName)> activeEmployees,
        IReadOnlyDictionary<Guid, Guid?>? parentById = null)
    {
        var ids = new HashSet<Guid>();
        var duties = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        parentById ??= new Dictionary<Guid, Guid?>();

        foreach (var e in activeEmployees)
        {
            if (!OrgChartPlacement.BelongsTo(unit, e.UnitId, e.FacilityId, parentById) || !ids.Add(e.Id))
                continue;

            var duty = string.IsNullOrWhiteSpace(e.DutyName) ? "Görevsiz" : e.DutyName!;
            duties[duty] = duties.TryGetValue(duty, out var c) ? c + 1 : 1;
        }

        var active = ids.Count;
        var missing = unit.IdealStaffCount.HasValue
            ? Math.Max(0, unit.IdealStaffCount.Value - active)
            : 0;

        var breakdown = duties
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => new NamedCountDto { Name = kv.Key, Count = kv.Value })
            .ToList();

        return new Result(active, missing, breakdown);
    }
}

internal static class OrgWriteRules
{
    public static async Task EnsureParentRulesAsync(
        AppDbContext db,
        Guid? parentId,
        OrganizationUnitType type,
        Guid? excludeId,
        CancellationToken ct)
    {
        if (type == OrganizationUnitType.Municipality)
        {
            if (parentId.HasValue)
                throw new AppValidationException("parentId", "Belediye kökünün üst birimi olamaz.");
            return;
        }

        if (!parentId.HasValue)
            throw new AppValidationException("parentId", "Üst birim zorunludur.");

        var parent = await db.OrganizationUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parentId.Value, ct)
            ?? throw new AppValidationException("parentId", "Üst birim bulunamadı.");

        if (parent.Type == OrganizationUnitType.Facility)
            throw new AppValidationException("parentId", "Tesis altında birim açılamaz.");

        if (type == OrganizationUnitType.Facility
            && parent.Type is not OrganizationUnitType.MainUnit
            && parent.Type is not OrganizationUnitType.SubUnit)
        {
            throw new AppValidationException(
                "parentId",
                "Tesis yalnızca bir ana birim veya alt birim altında açılabilir.");
        }

        if (excludeId.HasValue && parentId == excludeId)
            throw new AppValidationException("parentId", "Birim kendi üstü olamaz.");
    }

    public static async Task EnsureCodeUniqueAsync(AppDbContext db, string? code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = Normalize(code);
        if (normalized is null)
            return;

        var exists = await db.OrganizationUnits.AnyAsync(
            x => x.Code == normalized && (!excludeId.HasValue || x.Id != excludeId),
            ct);

        if (exists)
            throw new ConflictException($"Birim kodu zaten kullanılıyor: {normalized}");
    }

    public static async Task EnsureManagerExistsAsync(AppDbContext db, Guid? managerId, CancellationToken ct)
    {
        if (!managerId.HasValue)
            return;
        if (!await db.Employees.AnyAsync(x => x.Id == managerId, ct))
            throw new AppValidationException("managerEmployeeId", "Sorumlu personel bulunamadı.");
    }

    public static async Task EnsureFacilityCategoryExistsAsync(
        AppDbContext db,
        OrganizationUnitType type,
        Guid? categoryId,
        CancellationToken ct)
    {
        if (type != OrganizationUnitType.Facility || !categoryId.HasValue)
            return;
        if (!await db.FacilityCategories.AnyAsync(x => x.Id == categoryId && x.IsActive, ct))
            throw new AppValidationException("facilityCategoryId", "Tesis türü bulunamadı veya aktif değil.");
    }

    public static async Task<bool> IsDescendantAsync(AppDbContext db, Guid ancestorId, Guid candidateId, CancellationToken ct)
    {
        var currentId = candidateId;
        var guard = 0;
        while (guard++ < 50)
        {
            var parentId = await db.OrganizationUnits
                .AsNoTracking()
                .Where(x => x.Id == currentId)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(ct);

            if (parentId is null)
                return false;
            if (parentId == ancestorId)
                return true;
            currentId = parentId.Value;
        }
        return false;
    }

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class OrgLabels
{
    public static string Type(OrganizationUnitType t) => t switch
    {
        OrganizationUnitType.Municipality => "Belediye",
        OrganizationUnitType.DeputyPresidency => "Başkan Yardımcılığı",
        OrganizationUnitType.Directorate => "Müdürlük",
        OrganizationUnitType.MainUnit => "Ana birim",
        OrganizationUnitType.SubUnit => "Alt birim",
        OrganizationUnitType.Facility => "Tesis",
        _ => t.ToString()
    };

    public static string Status(OrganizationUnitStatus s) => s switch
    {
        OrganizationUnitStatus.Active => "Aktif",
        OrganizationUnitStatus.Passive => "Pasif",
        OrganizationUnitStatus.Closed => "Kapalı",
        OrganizationUnitStatus.UnderRenovation => "Tadilatta",
        OrganizationUnitStatus.TemporarilyClosed => "Geçici kapalı",
        OrganizationUnitStatus.Planning => "Planlama aşamasında",
        OrganizationUnitStatus.Transferred => "Devredildi",
        OrganizationUnitStatus.OutOfUse => "Kullanım dışı",
        _ => s.ToString()
    };

    public static string EmployeeStatus(EmployeeStatus s) => s switch
    {
        Domain.Enums.EmployeeStatus.Active => "Aktif",
        Domain.Enums.EmployeeStatus.Passive => "Pasif",
        Domain.Enums.EmployeeStatus.OnLeave => "İzinli",
        Domain.Enums.EmployeeStatus.LongTermLeave => "Uzun süreli izinli",
        Domain.Enums.EmployeeStatus.TemporaryAssignment => "Geçici görevli",
        Domain.Enums.EmployeeStatus.LeftJob => "İşten ayrıldı",
        Domain.Enums.EmployeeStatus.Retired => "Emekli",
        Domain.Enums.EmployeeStatus.TransferredToOtherDirectorate => "Başka müdürlüğe geçti",
        Domain.Enums.EmployeeStatus.Suspended => "Askıda",
        Domain.Enums.EmployeeStatus.AwaitingReturn => "Göreve dönmesi bekleniyor",
        _ => s.ToString()
    };

    public static string Movement(MovementType t) => t switch
    {
        MovementType.UnitChange => "Birim değişikliği",
        MovementType.FacilityChange => "Tesis değişikliği",
        MovementType.DutyChange => "Görev değişikliği",
        MovementType.TitleChange => "Unvan değişikliği",
        MovementType.TemporaryAssignment => "Geçici görevlendirme",
        MovementType.PermanentAssignment => "Kalıcı görevlendirme",
        MovementType.AdditionalDutyAssigned => "Ek görev verilmesi",
        MovementType.DutyRemoved => "Görevden alınma",
        MovementType.TransferToOtherDirectorate => "Başka müdürlüğe geçiş",
        MovementType.LeftJob => "İşten ayrılma",
        MovementType.Retirement => "Emeklilik",
        MovementType.ReturnToDuty => "Göreve dönüş",
        _ => t.ToString()
    };
}
