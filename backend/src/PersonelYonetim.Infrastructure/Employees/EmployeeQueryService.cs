using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

/// <summary>
/// Personel listesi / detay — filtre + sayfalama + alan maskeleme.
/// Hassas alanlar yetki yoksa DTO'ya hiç yazılmaz (frontend gizlemesi yetmez).
/// </summary>
public sealed class EmployeeQueryService : IEmployeeQueryService
{
    private const byte IncompleteProfileThreshold = 80;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly ISensitiveAccessLogger _sensitiveAccess;

    public EmployeeQueryService(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        ISensitiveAccessLogger sensitiveAccess)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _sensitiveAccess = sensitiveAccess;
    }

    public async Task<PagedResult<EmployeeListItemDto>> GetListAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var employees = _db.Employees
            .AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Facility)
            .Include(x => x.EmploymentType)
            .Include(x => x.JobTitle)
            .Include(x => x.Assignments)
            .ThenInclude(a => a.JobDuty)
            .Include(x => x.SpecialConditions)
            .AsQueryable();

        employees = await ApplyUnitScopeAsync(employees, cancellationToken);
        if (employees is null)
        {
            return new PagedResult<EmployeeListItemDto>
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            employees = employees.Where(x =>
                x.FirstName.ToLower().Contains(term) ||
                x.LastName.ToLower().Contains(term) ||
                (x.FirstName + " " + x.LastName).ToLower().Contains(term) ||
                (x.EmployeeNumber != null && x.EmployeeNumber.ToLower().Contains(term)) ||
                (x.PersonalPhone != null && x.PersonalPhone.Contains(term)) ||
                (x.CorporatePhone != null && x.CorporatePhone.Contains(term)) ||
                (x.PersonalEmail != null && x.PersonalEmail.ToLower().Contains(term)) ||
                (x.CorporateEmail != null && x.CorporateEmail.ToLower().Contains(term)) ||
                (x.JobTitle != null && x.JobTitle.Name.ToLower().Contains(term)) ||
                (x.Unit != null && x.Unit.Name.ToLower().Contains(term)) ||
                (x.Facility != null && x.Facility.Name.ToLower().Contains(term)) ||
                (x.EmploymentType != null && x.EmploymentType.Name.ToLower().Contains(term)) ||
                x.Assignments.Any(a => a.JobDuty != null && a.JobDuty.Name.ToLower().Contains(term)) ||
                x.EducationRecords.Any(e =>
                    (e.Department != null && e.Department.ToLower().Contains(term)) ||
                    (e.University != null && e.University.ToLower().Contains(term)) ||
                    (e.School != null && e.School.ToLower().Contains(term))) ||
                x.Skills.Any(s => s.Skill != null && s.Skill.Name.ToLower().Contains(term)));
        }

        if (query.UnitId.HasValue)
        {
            if (query.IncludeSubUnits)
            {
                var unitIds = await GetUnitAndDescendantIdsAsync(query.UnitId.Value, cancellationToken);
                employees = employees.Where(x => x.UnitId != null && unitIds.Contains(x.UnitId.Value));
            }
            else
            {
                employees = employees.Where(x => x.UnitId == query.UnitId);
            }
        }

        if (query.FacilityId.HasValue)
            employees = employees.Where(x => x.FacilityId == query.FacilityId);

        if (query.EmploymentTypeId.HasValue)
            employees = employees.Where(x => x.EmploymentTypeId == query.EmploymentTypeId);

        if (query.JobTitleId.HasValue)
            employees = employees.Where(x => x.JobTitleId == query.JobTitleId);

        if (query.JobDutyId.HasValue)
            employees = employees.Where(x =>
                x.Assignments.Any(a => a.JobDutyId == query.JobDutyId && a.EndDate == null));

        if (query.DutyCategory.HasValue)
            employees = employees.Where(x =>
                x.Assignments.Any(a =>
                    a.EndDate == null &&
                    a.JobDuty != null &&
                    a.JobDuty.Category == query.DutyCategory));

        if (query.EducationLevel.HasValue)
            employees = employees.Where(x =>
                x.EducationRecords.Any(e => e.Level == query.EducationLevel));

        if (query.SkillId.HasValue)
            employees = employees.Where(x => x.Skills.Any(s => s.SkillId == query.SkillId));

        if (query.Status.HasValue)
            employees = employees.Where(x => x.Status == query.Status);

        if (query.IncompleteProfileOnly)
            employees = employees.Where(x => x.ProfileCompletionPercent < IncompleteProfileThreshold);

        if (query.MissingSkillsOnly)
            employees = employees.Where(x => !x.Skills.Any());

        if (query.MissingPhoneOnly)
            employees = employees.Where(x =>
                (x.PersonalPhone == null || x.PersonalPhone == string.Empty) &&
                (x.CorporatePhone == null || x.CorporatePhone == string.Empty));

        if (query.MissingFacilityOnly)
            employees = employees.Where(x => x.FacilityId == null || x.Facility == null);

        if (query.HasSpecialConditionOnly)
            employees = employees.Where(x => x.SpecialConditions.Any());

        if (query.HireYearFrom is int yearFrom and > 0)
            employees = employees.Where(x => x.HireDate != null && x.HireDate.Value.Year >= yearFrom);

        if (query.HireYearTo is int yearTo and > 0)
            employees = employees.Where(x => x.HireDate != null && x.HireDate.Value.Year <= yearTo);

        if (!string.IsNullOrWhiteSpace(query.UniversityContains))
        {
            var uni = query.UniversityContains.Trim().ToLower();
            employees = employees.Where(x =>
                x.EducationRecords.Any(e => e.University != null && e.University.ToLower().Contains(uni)));
        }

        if (query.GraduationYearFrom is int gradFrom and > 0)
            employees = employees.Where(x =>
                x.EducationRecords.Any(e => e.GraduationYear != null && e.GraduationYear >= gradFrom));

        if (query.GraduationYearTo is int gradTo and > 0)
            employees = employees.Where(x =>
                x.EducationRecords.Any(e => e.GraduationYear != null && e.GraduationYear <= gradTo));

        if (query.HasNotesOnly)
            employees = employees.Where(x => x.Notes.Any());

        if (query.MissingCertificatesOnly)
            employees = employees.Where(x => !x.Certificates.Any());

        if (query.ExpiredCertificateOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            employees = employees.Where(x =>
                x.Certificates.Any(c => c.ExpiresOn != null && c.ExpiresOn < today));
        }

        employees = ApplySort(employees, query.SortBy, query.SortDesc);

        var total = await employees.CountAsync(cancellationToken);

        var rows = await employees
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var canViewPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);

        var items = rows.Select(x =>
        {
            var primaryDuty = x.Assignments
                .Where(a => a.IsPrimary && a.EndDate == null)
                .Select(a => a.JobDuty?.Name)
                .FirstOrDefault()
                ?? x.Assignments
                    .Where(a => a.EndDate == null)
                    .Select(a => a.JobDuty?.Name)
                    .FirstOrDefault();
            return new EmployeeListItemDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                FullName = x.FullName,
                EmployeeNumber = x.EmployeeNumber,
                HasPhoto = !string.IsNullOrWhiteSpace(x.PhotoPath),
                JobTitleName = x.JobTitle?.Name,
                PrimaryDutyName = primaryDuty,
                UnitName = x.Unit?.Name,
                FacilityName = x.Facility?.Name,
                EmploymentTypeName = x.EmploymentType?.Name,
                Status = x.Status,
                StatusLabel = ToStatusLabel(x.Status),
                HireDate = x.HireDate,
                ProfileCompletionPercent = x.ProfileCompletionPercent,
                UpdatedAtUtc = x.UpdatedAtUtc ?? x.CreatedAtUtc,
                PersonalPhone = canViewPhone ? x.PersonalPhone : null,
                CorporatePhone = canViewPhone ? x.CorporatePhone : null,
                HasSpecialCondition = x.SpecialConditions.Count > 0
            };
        }).ToList();

        return new PagedResult<EmployeeListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<EmployeeDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = _db.Employees
            .AsNoTracking()
            .Include(x => x.Unit)
            .ThenInclude(u => u!.ManagerEmployee)
            .Include(x => x.Facility)
            .Include(x => x.EmploymentType)
            .Include(x => x.JobTitle)
            .Include(x => x.ManagerEmployee)
            .Include(x => x.Assignments)
            .ThenInclude(a => a.JobDuty)
            .Include(x => x.EducationRecords)
            .Include(x => x.Skills)
            .ThenInclude(s => s.Skill)
            .Include(x => x.Certificates)
            .ThenInclude(c => c.RelatedSkill)
            .Include(x => x.Movements)
            .Include(x => x.Notes)
            .Include(x => x.SpecialConditions)
            .Include(x => x.SensitiveData)
            .Where(x => x.Id == id);

        query = await ApplyUnitScopeAsync(query, cancellationToken);
        if (query is null)
            return null;

        var x = await query.FirstOrDefaultAsync(cancellationToken);
        if (x is null)
            return null;

        var canViewPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canViewAddress = _currentUser.HasPermission(PermissionCodes.EmployeesViewAddress);
        var canViewNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);
        var canViewSpecial = _currentUser.HasPermission(PermissionCodes.EmployeesViewSpecialConditions);

        var primaryAssignment = x.Assignments
            .Where(a => a.IsPrimary && a.EndDate is null)
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();
        var primaryDuty = primaryAssignment?.JobDuty?.Name;
        var primaryDutyCategory = primaryAssignment?.JobDuty is null
            ? null
            : ToDutyCategoryLabel(primaryAssignment.JobDuty.Category);

        var (nationalIdDisplay, nationalIdMasked) = BuildNationalIdDisplay(
            x.SensitiveData?.NationalIdEncrypted,
            canViewNationalId);

        var orgPath = await ResolveOrgPathAsync(x.UnitId, cancellationToken);
        var unitSupervisor = x.Unit?.ManagerEmployee is null
            ? null
            : $"{x.Unit.ManagerEmployee.FirstName} {x.Unit.ManagerEmployee.LastName}".Trim();

        var movementLookups = await LoadMovementLookupsAsync(x.Movements, cancellationToken);

        // Hassas görüntüleme denetimi — TCKN / özel durum (telefon-adres her açılışta gürültü yaratır)
        if (canViewNationalId && !nationalIdMasked && !string.IsNullOrWhiteSpace(nationalIdDisplay))
        {
            await _sensitiveAccess.LogAsync(
                SensitiveAccessActions.ViewNationalId,
                "Employee",
                x.Id.ToString(),
                new { field = "NationalId" },
                cancellationToken);
        }

        if (canViewSpecial && x.SpecialConditions.Count > 0)
        {
            await _sensitiveAccess.LogAsync(
                SensitiveAccessActions.ViewSpecialConditions,
                "Employee",
                x.Id.ToString(),
                new { count = x.SpecialConditions.Count },
                cancellationToken);
        }

        return new EmployeeDetailDto
        {
            Id = x.Id,
            FirstName = x.FirstName,
            LastName = x.LastName,
            FullName = x.FullName,
            EmployeeNumber = x.EmployeeNumber,
            HasPhoto = !string.IsNullOrWhiteSpace(x.PhotoPath),
            Status = x.Status,
            StatusLabel = ToStatusLabel(x.Status),
            ProfileCompletionPercent = x.ProfileCompletionPercent,
            General = new EmployeeGeneralInfoDto
            {
                BirthDate = x.BirthDate,
                GenderLabel = ToGenderLabel(x.Gender),
                PersonalPhone = canViewPhone ? x.PersonalPhone : null,
                CorporatePhone = canViewPhone ? x.CorporatePhone : null,
                PersonalEmail = x.PersonalEmail,
                CorporateEmail = x.CorporateEmail,
                Address = canViewAddress ? x.Address : null,
                EmergencyContactName = canViewPhone ? x.EmergencyContactName : null,
                EmergencyContactPhone = canViewPhone ? x.EmergencyContactPhone : null,
                NationalIdDisplay = nationalIdDisplay,
                NationalIdIsMasked = nationalIdMasked
            },
            Corporate = new EmployeeCorporateInfoDto
            {
                UnitId = x.UnitId,
                DirectorateName = orgPath.DirectorateName,
                MainUnitName = orgPath.MainUnitName,
                SubUnitName = orgPath.SubUnitName,
                UnitName = x.Unit?.Name,
                FacilityName = x.Facility?.Name,
                EmploymentTypeName = x.EmploymentType?.Name,
                JobTitleName = x.JobTitle?.Name,
                PrimaryDutyName = primaryDuty,
                PrimaryDutyCategoryLabel = primaryDutyCategory,
                ManagerName = x.ManagerEmployee is null
                    ? null
                    : $"{x.ManagerEmployee.FirstName} {x.ManagerEmployee.LastName}".Trim(),
                UnitSupervisorName = unitSupervisor,
                HireDate = x.HireDate,
                DirectorateStartDate = x.DirectorateStartDate,
                UnitStartDate = x.UnitStartDate,
                DutyStartDate = x.DutyStartDate
            },
            Assignments = x.Assignments
                .OrderByDescending(a => a.IsPrimary)
                .ThenByDescending(a => a.StartDate)
                .Select(a => new EmployeeAssignmentDto
                {
                    Id = a.Id,
                    JobDutyId = a.JobDutyId,
                    DutyName = a.JobDuty?.Name ?? "—",
                    CategoryLabel = ToDutyCategoryLabel(a.JobDuty?.Category ?? DutyCategory.Other),
                    IsPrimary = a.IsPrimary,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    Description = a.Description
                })
                .ToList(),
            Education = x.EducationRecords
                .OrderByDescending(e => e.GraduationYear)
                .Select(e => new EmployeeEducationDto
                {
                    Id = e.Id,
                    Level = e.Level,
                    LevelLabel = ToEducationLevelLabel(e.Level),
                    University = e.University,
                    Faculty = e.Faculty,
                    School = e.School,
                    Department = e.Department,
                    Program = e.Program,
                    GraduationYear = e.GraduationYear,
                    CompletionStatus = e.CompletionStatus,
                    CompletionStatusLabel = ToEducationCompletionLabel(e.CompletionStatus),
                    DiplomaNumber = e.DiplomaNumber,
                    Description = e.Description
                })
                .ToList(),
            Skills = x.Skills
                .OrderBy(s => s.Skill.Name)
                .Select(s => new EmployeeSkillDto
                {
                    Id = s.Id,
                    SkillId = s.SkillId,
                    Name = s.Skill.Name,
                    CategoryLabel = ToSkillCategoryLabel(s.Skill.Category),
                    Level = s.Level,
                    LevelLabel = ToSkillLevelLabel(s.Level),
                    ExperienceDuration = s.ExperienceDuration,
                    HasCertificate = s.HasCertificate,
                    CertificateDate = s.CertificateDate,
                    CertificateIssuer = s.CertificateIssuer,
                    Description = s.Description
                })
                .ToList(),
            Certificates = x.Certificates
                .OrderByDescending(c => c.IssuedOn)
                .Select(c => new EmployeeCertificateDto
                {
                    Id = c.Id,
                    CertificateDefinitionId = c.CertificateDefinitionId,
                    Name = c.Name,
                    Issuer = c.Issuer,
                    Category = c.Category,
                    IssuedOn = c.IssuedOn,
                    ExpiresOn = c.ExpiresOn,
                    DocumentNumber = c.DocumentNumber,
                    Description = c.Description,
                    RelatedSkillId = c.RelatedSkillId,
                    RelatedSkillName = c.RelatedSkill != null ? c.RelatedSkill.Name : null
                })
                .ToList(),
            Movements = x.Movements
                .OrderByDescending(m => m.StartDate)
                .Select(m => new EmployeeMovementDto
                {
                    Id = m.Id,
                    MovementType = m.MovementType,
                    MovementTypeLabel = ToMovementLabel(m.MovementType),
                    OldUnitId = m.OldUnitId,
                    OldUnitName = LookupName(movementLookups.Units, m.OldUnitId),
                    NewUnitId = m.NewUnitId,
                    NewUnitName = LookupName(movementLookups.Units, m.NewUnitId),
                    OldFacilityId = m.OldFacilityId,
                    OldFacilityName = LookupName(movementLookups.Units, m.OldFacilityId),
                    NewFacilityId = m.NewFacilityId,
                    NewFacilityName = LookupName(movementLookups.Units, m.NewFacilityId),
                    OldJobTitleId = m.OldJobTitleId,
                    OldJobTitleName = LookupName(movementLookups.Titles, m.OldJobTitleId),
                    NewJobTitleId = m.NewJobTitleId,
                    NewJobTitleName = LookupName(movementLookups.Titles, m.NewJobTitleId),
                    OldJobDutyId = m.OldJobDutyId,
                    OldJobDutyName = LookupName(movementLookups.Duties, m.OldJobDutyId),
                    NewJobDutyId = m.NewJobDutyId,
                    NewJobDutyName = LookupName(movementLookups.Duties, m.NewJobDutyId),
                    StartDate = m.StartDate,
                    EndDate = m.EndDate,
                    Reason = m.Reason,
                    Description = m.Description,
                    ApprovedBy = m.ApprovedBy,
                    CreatedBy = m.CreatedBy
                })
                .ToList(),
            Notes = x.Notes
                .Where(n => NoteAccessRules.CanView(n, _currentUser))
                .OrderByDescending(n => n.NoteDateUtc)
                .Select(n => new EmployeeNoteDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Category = n.Category,
                    CategoryLabel = ToNoteCategoryLabel(n.Category),
                    Content = n.Content,
                    NoteDateUtc = n.NoteDateUtc,
                    Visibility = n.Visibility,
                    VisibilityLabel = ToNoteVisibilityLabel(n.Visibility),
                    ReminderDate = n.ReminderDate,
                    CreatedBy = n.CreatedBy,
                    CanModify = NoteAccessRules.CanModify(n, _currentUser)
                })
                .ToList(),
            HasSpecialCondition = x.SpecialConditions.Count > 0,
            SpecialConditions = canViewSpecial
                ? x.SpecialConditions.Select(s => new EmployeeSpecialConditionDto
                {
                    Id = s.Id,
                    ConditionType = s.ConditionType,
                    Description = s.Description,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    IsPermanent = s.IsPermanent,
                    RequiresDutyAdjustment = s.RequiresDutyAdjustment,
                    RequiresWorkspaceAdjustment = s.RequiresWorkspaceAdjustment,
                    HasDocument = s.HasDocument
                }).ToList()
                : null
        };
    }

    private sealed record OrgPath(string? DirectorateName, string? MainUnitName, string? SubUnitName);

    private sealed record MovementLookups(
        IReadOnlyDictionary<Guid, string> Units,
        IReadOnlyDictionary<Guid, string> Titles,
        IReadOnlyDictionary<Guid, string> Duties);

    private static string? LookupName(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id.HasValue && map.TryGetValue(id.Value, out var name) ? name : null;

    private async Task<OrgPath> ResolveOrgPathAsync(Guid? unitId, CancellationToken cancellationToken)
    {
        if (unitId is null)
            return new OrgPath(null, null, null);

        var units = await _db.OrganizationUnits
            .AsNoTracking()
            .Select(u => new { u.Id, u.Name, u.Type, u.ParentId })
            .ToListAsync(cancellationToken);

        var byId = units.ToDictionary(u => u.Id);
        string? directorate = null;
        string? mainUnit = null;
        string? subUnit = null;

        var currentId = unitId;
        var guard = 0;
        while (currentId.HasValue && guard++ < 16)
        {
            if (!byId.TryGetValue(currentId.Value, out var node))
                break;

            switch (node.Type)
            {
                case OrganizationUnitType.Directorate:
                    directorate ??= node.Name;
                    break;
                case OrganizationUnitType.MainUnit:
                    mainUnit ??= node.Name;
                    break;
                case OrganizationUnitType.SubUnit:
                    subUnit ??= node.Name;
                    break;
            }

            currentId = node.ParentId;
        }

        return new OrgPath(directorate, mainUnit, subUnit);
    }

    private async Task<MovementLookups> LoadMovementLookupsAsync(
        IEnumerable<Domain.Entities.EmployeeMovement> movements,
        CancellationToken cancellationToken)
    {
        var unitIds = movements
            .SelectMany(m => new Guid?[] { m.OldUnitId, m.NewUnitId, m.OldFacilityId, m.NewFacilityId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var titleIds = movements
            .SelectMany(m => new Guid?[] { m.OldJobTitleId, m.NewJobTitleId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var dutyIds = movements
            .SelectMany(m => new Guid?[] { m.OldJobDutyId, m.NewJobDutyId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var units = unitIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.OrganizationUnits.AsNoTracking()
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var titles = titleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.JobTitles.AsNoTracking()
                .Where(t => titleIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var duties = dutyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.JobDuties.AsNoTracking()
                .Where(d => dutyIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return new MovementLookups(units, titles, duties);
    }

    private async Task<IQueryable<Domain.Entities.Employee>?> ApplyUnitScopeAsync(
        IQueryable<Domain.Entities.Employee> employees,
        CancellationToken cancellationToken)
    {
        if (_currentUser.HasPermission(PermissionCodes.EmployeesViewAllUnits))
            return employees;

        var unitId = await GetCurrentUserUnitIdAsync(cancellationToken);
        if (unitId is null)
            return null;

        return employees.Where(x => x.UnitId == unitId);
    }

    private async Task<HashSet<Guid>> GetUnitAndDescendantIdsAsync(
        Guid rootUnitId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.OrganizationUnits
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = rows
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<Guid> { rootUnitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var childId in children)
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }

    private async Task<Guid?> GetCurrentUserUnitIdAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.UserId)
            .Select(x => x.Employee != null ? x.Employee.UnitId : null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private (string? Display, bool IsMasked) BuildNationalIdDisplay(string? stored, bool canViewFull)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return (null, false);

        var plain = _nationalId.Unprotect(stored);
        if (string.IsNullOrWhiteSpace(plain))
            return ("***********", true);

        if (canViewFull)
            return (plain, false);

        return (_nationalId.Mask(plain), true);
    }

    private static IQueryable<Domain.Entities.Employee> ApplySort(
        IQueryable<Domain.Entities.Employee> query,
        string? sortBy,
        bool sortDesc)
    {
        return (sortBy?.ToLowerInvariant()) switch
        {
            "firstname" => sortDesc ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName),
            "hiredate" => sortDesc ? query.OrderByDescending(x => x.HireDate) : query.OrderBy(x => x.HireDate),
            "status" => sortDesc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "completion" => sortDesc
                ? query.OrderByDescending(x => x.ProfileCompletionPercent)
                : query.OrderBy(x => x.ProfileCompletionPercent),
            "updated" => sortDesc
                ? query.OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                : query.OrderBy(x => x.UpdatedAtUtc ?? x.CreatedAtUtc),
            _ => sortDesc
                ? query.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName)
                : query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
        };
    }

    private static string ToStatusLabel(EmployeeStatus status) => EmployeeStatusLabels.For(status);

    private static string ToGenderLabel(Gender gender) => gender switch
    {
        Gender.Female => "Kadın",
        Gender.Male => "Erkek",
        Gender.Other => "Diğer",
        _ => "Belirtilmemiş"
    };

    private static string ToDutyCategoryLabel(DutyCategory category) => category switch
    {
        DutyCategory.Manager => "Yönetici",
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
        _ => "Diğer"
    };

    private static string ToEducationLevelLabel(EducationLevel level) => level switch
    {
        EducationLevel.Primary => "İlköğretim",
        EducationLevel.HighSchool => "Lise",
        EducationLevel.AssociateDegree => "Ön lisans",
        EducationLevel.Bachelor => "Lisans",
        EducationLevel.Master => "Yüksek lisans",
        EducationLevel.Doctorate => "Doktora",
        _ => "Bilinmiyor"
    };

    private static string ToEducationCompletionLabel(EducationCompletionStatus status) => status switch
    {
        EducationCompletionStatus.Ongoing => "Devam ediyor",
        EducationCompletionStatus.Graduated => "Mezun",
        EducationCompletionStatus.Suspended => "Kayıt dondurmuş",
        EducationCompletionStatus.DroppedOut => "Yarım bırakmış",
        _ => "Bilgi yok"
    };

    private static string ToSkillCategoryLabel(SkillCategory category) => category switch
    {
        SkillCategory.Technical => "Teknik",
        SkillCategory.EducationWorkshop => "Eğitim / Atölye",
        SkillCategory.Administrative => "İdari",
        SkillCategory.Communication => "İletişim",
        SkillCategory.Language => "Dil",
        _ => category.ToString()
    };

    private static string ToSkillLevelLabel(SkillLevel level) => level switch
    {
        SkillLevel.Beginner => "Başlangıç",
        SkillLevel.Intermediate => "Orta",
        SkillLevel.Advanced => "İleri",
        SkillLevel.Expert => "Uzman",
        _ => level.ToString()
    };

    private static string ToMovementLabel(MovementType type) => type switch
    {
        MovementType.UnitChange => "Birim değişikliği",
        MovementType.FacilityChange => "Tesis değişikliği",
        MovementType.DutyChange => "Görev değişikliği",
        MovementType.TitleChange => "Unvan değişikliği",
        MovementType.TemporaryAssignment => "Geçici görevlendirme",
        MovementType.PermanentAssignment => "Kalıcı görevlendirme",
        MovementType.AdditionalDutyAssigned => "Ek görev",
        MovementType.DutyRemoved => "Görevden alma",
        MovementType.TransferToOtherDirectorate => "Başka müdürlüğe geçiş",
        MovementType.LeftJob => "İşten ayrılma",
        MovementType.Retirement => "Emeklilik",
        MovementType.ReturnToDuty => "Göreve dönüş",
        _ => type.ToString()
    };

    private static string ToNoteCategoryLabel(NoteCategory category) => category switch
    {
        NoteCategory.General => "Genel",
        NoteCategory.Manager => "Yönetici",
        NoteCategory.Interview => "Görüşme",
        NoteCategory.Assignment => "Görevlendirme",
        NoteCategory.Performance => "Performans",
        NoteCategory.Training => "Eğitim",
        NoteCategory.Communication => "İletişim",
        NoteCategory.Duty => "Görev",
        NoteCategory.SpecialSituation => "Özel durum",
        NoteCategory.Reminder => "Hatırlatma",
        NoteCategory.InstitutionalDevelopment => "Kurumsal gelişim",
        _ => category.ToString()
    };

    private static string ToNoteVisibilityLabel(NoteVisibility visibility) => visibility switch
    {
        NoteVisibility.AuthorOnly => "Yalnızca yazar",
        NoteVisibility.UnitManagers => "Birim yöneticileri",
        NoteVisibility.DirectorateManagers => "Müdürlük yöneticileri",
        NoteVisibility.PrivilegedUsers => "Yetkili kullanıcılar",
        NoteVisibility.SystemAdministrators => "Sistem yöneticileri",
        _ => visibility.ToString()
    };
}
