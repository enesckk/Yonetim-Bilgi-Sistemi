using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Employees;

/// <summary>
/// Personel MediatR handlers — eski EmployeeCommandService.
/// CreateEmployeeRequest = IRequest&lt;Guid&gt; (Import da aynı mesajı Send eder).
/// </summary>
public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeRequest, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IMemoryCache _cache;

    public CreateEmployeeHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        IUserNotificationService notifications,
        IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<Guid> Handle(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesCreate))
            throw new ForbiddenException("Personel eklemek için Employees.Create gerekir.");

        await EmployeeWriteHelpers.EnsureLookupsExistAsync(
            _db, request.UnitId, request.FacilityId, request.EmploymentTypeId,
            request.JobTitleId, request.ManagerEmployeeId, cancellationToken);
        var createScope = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (!UnitScopeHelper.IsUnitAllowed(createScope, request.UnitId, request.FacilityId))
            throw new ForbiddenException("Personeli yalnızca kendi tesis kapsamınıza ekleyebilirsiniz.");
        await EmployeeWriteHelpers.EnsurePrimaryDutyExistsAsync(
            _db, request.PrimaryJobDutyId, cancellationToken);
        await EmployeeWriteHelpers.EnsureEmployeeNumberUniqueAsync(
            _db, request.EmployeeNumber, excludeId: null, cancellationToken);

        var canPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canAddress = _currentUser.HasPermission(PermissionCodes.EmployeesViewAddress);
        var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);

        if (canNationalId)
        {
            await EmployeeWriteHelpers.EnsureNationalIdUniqueAsync(
                _db, _nationalId, request.NationalId, excludeEmployeeId: null, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            throw new ForbiddenException("T.C. kimlik bilgisi kaydetmek için Employees.ViewNationalId gerekir.");
        }

        if (!canPhone
            && (!string.IsNullOrWhiteSpace(request.PersonalPhone)
                || !string.IsNullOrWhiteSpace(request.CorporatePhone)
                || !string.IsNullOrWhiteSpace(request.EmergencyContactPhone)))
        {
            throw new ForbiddenException("Telefon bilgisi kaydetmek için Employees.ViewPhone gerekir.");
        }

        if (!_currentUser.HasPermission(PermissionCodes.EmployeesSetStatus)
            && request.Status != EmployeeStatus.Active)
        {
            throw new ForbiddenException("Personel durumunu değiştirme yetkiniz yok. Yeni kayıt Aktif olarak açılır.");
        }

        var actor = _currentUser.UserName ?? "system";
        var employee = new Employee
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmployeeNumber = EmployeeWriteHelpers.NormalizeOptional(request.EmployeeNumber),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Status = request.Status,
            PersonalPhone = canPhone ? EmployeeWriteHelpers.NormalizeOptional(request.PersonalPhone) : null,
            CorporatePhone = canPhone ? EmployeeWriteHelpers.NormalizeOptional(request.CorporatePhone) : null,
            PersonalEmail = EmployeeWriteHelpers.NormalizeOptional(request.PersonalEmail),
            CorporateEmail = EmployeeWriteHelpers.NormalizeOptional(request.CorporateEmail),
            Address = canAddress
                ? EmployeeWriteHelpers.NormalizeOptional(request.Address)
                : null,
            EmergencyContactName = EmployeeWriteHelpers.NormalizeOptional(request.EmergencyContactName),
            EmergencyContactPhone = canPhone
                ? EmployeeWriteHelpers.NormalizeOptional(request.EmergencyContactPhone)
                : null,
            UnitId = request.UnitId,
            FacilityId = request.FacilityId,
            EmploymentTypeId = request.EmploymentTypeId,
            JobTitleId = request.JobTitleId,
            ManagerEmployeeId = request.ManagerEmployeeId,
            HireDate = request.HireDate,
            DirectorateStartDate = request.DirectorateStartDate,
            UnitStartDate = request.UnitStartDate,
            DutyStartDate = request.DutyStartDate,
            CreatedBy = actor
        };

        if (canNationalId && !string.IsNullOrWhiteSpace(request.NationalId))
        {
            var digits = EmployeeWriteHelpers.DigitsOnly(request.NationalId);
            employee.SensitiveData = new EmployeeSensitiveData
            {
                NationalIdEncrypted = _nationalId.Protect(digits),
                NationalIdHash = _nationalId.ComputeLookupHash(digits),
                CreatedBy = actor
            };
        }

        if (request.PrimaryJobDutyId.HasValue)
        {
            employee.Assignments.Add(new EmployeeAssignment
            {
                JobDutyId = request.PrimaryJobDutyId.Value,
                IsPrimary = true,
                StartDate = request.DutyStartDate ?? request.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedBy = actor
            });
        }

        employee.ProfileCompletionPercent = EmployeeWriteHelpers.CalculateProfileCompletion(
            employee,
            employee.SensitiveData?.NationalIdEncrypted);

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateOrgLookups();

        await _notifications.NotifyUsersWithPermissionAsync(
            PermissionCodes.EmployeesView,
            "Yeni personel kaydı oluşturuldu",
            $"{employee.FullName} sisteme eklendi{(string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? "" : $" ({employee.EmployeeNumber})")}.",
            NotificationSeverity.Success,
            Application.Features.Notifications.NotificationCategories.Employees,
            $"/employees/{employee.Id}",
            cancellationToken);

        return employee.Id;
    }
}

public sealed class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IMemoryCache _cache;

    public UpdateEmployeeHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        IUserNotificationService notifications,
        IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesUpdate))
            throw new ForbiddenException("Personel güncellemek için Employees.Update gerekir.");

        var employee = await _db.Employees
            .Include(x => x.SensitiveData)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı.");

        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (!UnitScopeHelper.IsUnitAllowed(allowed, employee.UnitId, employee.FacilityId))
            throw new ForbiddenException("Bu personel sizin tesis kapsamınızda değil.");
        if (!UnitScopeHelper.IsUnitAllowed(allowed, request.UnitId, request.FacilityId))
            throw new ForbiddenException("Personeli kapsamınız dışındaki birime taşıyamazsınız.");

        var previousStatus = employee.Status;

        if (employee.Status != request.Status
            && !_currentUser.HasPermission(PermissionCodes.EmployeesSetStatus))
        {
            throw new ForbiddenException("Personel durumunu değiştirme yetkiniz yok.");
        }

        await EmployeeWriteHelpers.EnsureLookupsExistAsync(
            _db, request.UnitId, request.FacilityId, request.EmploymentTypeId,
            request.JobTitleId, request.ManagerEmployeeId, cancellationToken);
        await EmployeeWriteHelpers.EnsurePrimaryDutyExistsAsync(
            _db, request.PrimaryJobDutyId, cancellationToken);
        await EmployeeWriteHelpers.EnsureEmployeeNumberUniqueAsync(
            _db, request.EmployeeNumber, excludeId: request.Id, cancellationToken);

        if (request.ManagerEmployeeId == request.Id)
            throw new AppValidationException(nameof(request.ManagerEmployeeId), "Personel kendi yöneticisi olamaz.");

        var actor = _currentUser.UserName ?? "system";
        var canPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canAddress = _currentUser.HasPermission(PermissionCodes.EmployeesViewAddress);
        var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);

        if (canNationalId && request.NationalIdProvided && !string.IsNullOrWhiteSpace(request.NationalId))
            await EmployeeWriteHelpers.EnsureNationalIdUniqueAsync(
                _db, _nationalId, request.NationalId, excludeEmployeeId: request.Id, cancellationToken);

        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.EmployeeNumber = EmployeeWriteHelpers.NormalizeOptional(request.EmployeeNumber);
        employee.BirthDate = request.BirthDate;
        employee.Gender = request.Gender;
        employee.Status = request.Status;
        employee.PersonalEmail = EmployeeWriteHelpers.NormalizeOptional(request.PersonalEmail);
        employee.CorporateEmail = EmployeeWriteHelpers.NormalizeOptional(request.CorporateEmail);
        employee.EmergencyContactName = EmployeeWriteHelpers.NormalizeOptional(request.EmergencyContactName);
        employee.EmergencyContactPhone = EmployeeWriteHelpers.NormalizeOptional(request.EmergencyContactPhone);
        employee.UnitId = request.UnitId;
        employee.FacilityId = request.FacilityId;
        employee.EmploymentTypeId = request.EmploymentTypeId;
        employee.JobTitleId = request.JobTitleId;
        employee.ManagerEmployeeId = request.ManagerEmployeeId;
        employee.HireDate = request.HireDate;
        employee.DirectorateStartDate = request.DirectorateStartDate;
        employee.UnitStartDate = request.UnitStartDate;
        employee.DutyStartDate = request.DutyStartDate;

        if (canPhone)
        {
            employee.PersonalPhone = EmployeeWriteHelpers.NormalizeOptional(request.PersonalPhone);
            employee.CorporatePhone = EmployeeWriteHelpers.NormalizeOptional(request.CorporatePhone);
        }

        if (canAddress)
            employee.Address = EmployeeWriteHelpers.NormalizeOptional(request.Address);

        if (canNationalId && request.NationalIdProvided)
        {
            var digits = string.IsNullOrWhiteSpace(request.NationalId)
                ? null
                : EmployeeWriteHelpers.DigitsOnly(request.NationalId);

            if (employee.SensitiveData is null && digits is not null)
            {
                employee.SensitiveData = new EmployeeSensitiveData
                {
                    NationalIdEncrypted = _nationalId.Protect(digits),
                    NationalIdHash = _nationalId.ComputeLookupHash(digits),
                    CreatedBy = actor
                };
                // Önceden üretilmiş Guid + yalnızca navigation.Add → DetectChanges Modified sanır
                _db.EmployeeSensitiveData.Add(employee.SensitiveData);
            }
            else if (employee.SensitiveData is not null)
            {
                employee.SensitiveData.NationalIdEncrypted = digits is null ? null : _nationalId.Protect(digits);
                employee.SensitiveData.NationalIdHash = digits is null ? null : _nationalId.ComputeLookupHash(digits);
                employee.SensitiveData.UpdatedAtUtc = DateTime.UtcNow;
                employee.SensitiveData.UpdatedBy = actor;
            }
        }

        EmployeeWriteHelpers.SyncPrimaryAssignment(
            _db, employee, request.PrimaryJobDutyId,
            request.DutyStartDate ?? request.HireDate, actor);

        employee.ProfileCompletionPercent = EmployeeWriteHelpers.CalculateProfileCompletion(
            employee,
            employee.SensitiveData?.NationalIdEncrypted);
        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = actor;

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateOrgLookups();

        if (previousStatus != EmployeeStatus.Passive && request.Status == EmployeeStatus.Passive)
        {
            await _notifications.NotifyUsersWithPermissionAsync(
                PermissionCodes.EmployeesView,
                "Personel pasife alındı",
                $"{employee.FullName} durumu pasif olarak güncellendi.",
                NotificationSeverity.Warning,
                Application.Features.Notifications.NotificationCategories.Employees,
                $"/employees/{employee.Id}",
                cancellationToken);
        }
    }
}

public sealed class GetEmployeeForEditHandler : IRequestHandler<GetEmployeeForEditQuery, EmployeeEditDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;

    public GetEmployeeForEditHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
    }

    public async Task<EmployeeEditDto?> Handle(
        GetEmployeeForEditQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesUpdate))
            throw new ForbiddenException("Düzenleme formu için Employees.Update gerekir.");

        var employee = await _db.Employees
            .AsNoTracking()
            .Include(x => x.SensitiveData)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (employee is null)
            return null;

        var primaryDutyId = employee.Assignments
            .Where(a => a.IsPrimary && a.EndDate == null)
            .OrderByDescending(a => a.StartDate)
            .Select(a => (Guid?)a.JobDutyId)
            .FirstOrDefault();

        var canPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canAddress = _currentUser.HasPermission(PermissionCodes.EmployeesViewAddress);
        var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);

        return new EmployeeEditDto
        {
            Id = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            EmployeeNumber = employee.EmployeeNumber,
            BirthDate = employee.BirthDate,
            Gender = employee.Gender,
            Status = employee.Status,
            PersonalPhone = canPhone ? employee.PersonalPhone : null,
            CorporatePhone = canPhone ? employee.CorporatePhone : null,
            PersonalEmail = employee.PersonalEmail,
            CorporateEmail = employee.CorporateEmail,
            Address = canAddress ? employee.Address : null,
            EmergencyContactName = employee.EmergencyContactName,
            EmergencyContactPhone = employee.EmergencyContactPhone,
            NationalId = canNationalId
                ? _nationalId.Unprotect(employee.SensitiveData?.NationalIdEncrypted)
                : null,
            CanEditNationalId = canNationalId,
            CanEditAddress = canAddress,
            CanEditPhone = canPhone,
            CanChangeStatus = _currentUser.HasPermission(PermissionCodes.EmployeesSetStatus),
            UnitId = employee.UnitId,
            FacilityId = employee.FacilityId,
            EmploymentTypeId = employee.EmploymentTypeId,
            JobTitleId = employee.JobTitleId,
            ManagerEmployeeId = employee.ManagerEmployeeId,
            PrimaryJobDutyId = primaryDutyId,
            HireDate = employee.HireDate,
            DirectorateStartDate = employee.DirectorateStartDate,
            UnitStartDate = employee.UnitStartDate,
            DutyStartDate = employee.DutyStartDate
        };
    }
}

public sealed class GetEmployeeFormOptionsHandler
    : IRequestHandler<GetEmployeeFormOptionsQuery, EmployeeFormOptionsDto>
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserService _currentUser;

    public GetEmployeeFormOptionsHandler(AppDbContext db, IMemoryCache cache, ICurrentUserService currentUser)
    {
        _db = db;
        _cache = cache;
        _currentUser = currentUser;
    }

    public async Task<EmployeeFormOptionsDto> Handle(
        GetEmployeeFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _currentUser, cancellationToken);
        if (allowed is null)
            return await _cache.GetOrCreateAsync(
                AppCache.EmployeeFormOptions,
                AppCache.LookupTtl,
                ct => LoadAsync(null, ct),
                cancellationToken);

        return await LoadAsync(allowed, cancellationToken);
    }

    private async Task<EmployeeFormOptionsDto> LoadAsync(HashSet<Guid>? allowed, CancellationToken cancellationToken)
    {
        var unitsQ = _db.OrganizationUnits
            .AsNoTracking()
            .Where(x => x.Type != OrganizationUnitType.Facility);
        var facilitiesQ = _db.OrganizationUnits
            .AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility);
        if (allowed is not null)
        {
            unitsQ = unitsQ.Where(x => allowed.Contains(x.Id));
            facilitiesQ = facilitiesQ.Where(x => allowed.Contains(x.Id));
        }

        var units = await unitsQ
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ParentId = x.ParentId,
                Type = (byte)x.Type
            })
            .ToListAsync(cancellationToken);

        var facilities = await facilitiesQ
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ParentId = x.ParentId,
                Type = (byte)x.Type
            })
            .ToListAsync(cancellationToken);

        var employmentTypes = await _db.EmploymentTypes
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code })
            .ToListAsync(cancellationToken);

        var jobTitles = await _db.JobTitles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code })
            .ToListAsync(cancellationToken);

        var duties = await _db.JobDuties
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);

        var skills = await _db.Skills
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);

        var managersQ = _db.Employees
            .AsNoTracking()
            .Where(x => x.Status == EmployeeStatus.Active);
        if (allowed is not null)
        {
            managersQ = managersQ.Where(x =>
                (x.UnitId != null && allowed.Contains(x.UnitId.Value))
                || (x.FacilityId != null && allowed.Contains(x.FacilityId.Value)));
        }

        var managers = await managersQ
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new LookupItemDto
            {
                Id = x.Id,
                Name = x.LastName + " " + x.FirstName,
                Code = x.EmployeeNumber
            })
            .ToListAsync(cancellationToken);

        return new EmployeeFormOptionsDto
        {
            Units = units,
            Facilities = facilities,
            EmploymentTypes = employmentTypes,
            JobTitles = jobTitles,
            Duties = duties,
            Skills = skills,
            Managers = managers,
            DutyCategories =
            [
                new EnumOptionDto { Value = (int)DutyCategory.Manager, Label = "Yönetici" },
                new EnumOptionDto { Value = (int)DutyCategory.Administrative, Label = "İdari" },
                new EnumOptionDto { Value = (int)DutyCategory.Instructor, Label = "Eğitmen" },
                new EnumOptionDto { Value = (int)DutyCategory.Technical, Label = "Teknik" },
                new EnumOptionDto { Value = (int)DutyCategory.Reception, Label = "Danışma" },
                new EnumOptionDto { Value = (int)DutyCategory.Library, Label = "Kütüphane" },
                new EnumOptionDto { Value = (int)DutyCategory.Auxiliary, Label = "Yardımcı" },
                new EnumOptionDto { Value = (int)DutyCategory.Cleaning, Label = "Temizlik" },
                new EnumOptionDto { Value = (int)DutyCategory.Project, Label = "Proje" },
                new EnumOptionDto { Value = (int)DutyCategory.SocialMedia, Label = "Sosyal medya" },
                new EnumOptionDto { Value = (int)DutyCategory.PublicRelations, Label = "Halkla ilişkiler" },
                new EnumOptionDto { Value = (int)DutyCategory.Other, Label = "Diğer" }
            ],
            EducationLevels =
            [
                new EnumOptionDto { Value = (int)EducationLevel.Primary, Label = "İlköğretim" },
                new EnumOptionDto { Value = (int)EducationLevel.HighSchool, Label = "Lise" },
                new EnumOptionDto { Value = (int)EducationLevel.AssociateDegree, Label = "Ön lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Bachelor, Label = "Lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Master, Label = "Yüksek lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Doctorate, Label = "Doktora" }
            ],
            Genders =
            [
                new EnumOptionDto { Value = (int)Gender.Unspecified, Label = "Belirtilmemiş" },
                new EnumOptionDto { Value = (int)Gender.Female, Label = "Kadın" },
                new EnumOptionDto { Value = (int)Gender.Male, Label = "Erkek" },
                new EnumOptionDto { Value = (int)Gender.Other, Label = "Diğer" }
            ],
            Statuses = Enum.GetValues<EmployeeStatus>()
                .Select(s => new EnumOptionDto { Value = (int)s, Label = EmployeeStatusLabels.For(s) })
                .ToList()
        };
    }
}

internal static class EmployeeWriteHelpers
{
    public static async Task EnsureEmployeeNumberUniqueAsync(
        AppDbContext db, string? employeeNumber, Guid? excludeId, CancellationToken ct)
    {
        var normalized = NormalizeOptional(employeeNumber);
        if (normalized is null)
            return;

        var exists = await db.Employees.AnyAsync(
            x => x.EmployeeNumber == normalized && (!excludeId.HasValue || x.Id != excludeId),
            ct);

        if (exists)
            throw new ConflictException($"'{normalized}' sicil numarası zaten kullanılıyor.");
    }

    public static async Task EnsureNationalIdUniqueAsync(
        AppDbContext db,
        INationalIdProtector nationalId,
        string? rawNationalId,
        Guid? excludeEmployeeId,
        CancellationToken ct)
    {
        var hash = nationalId.ComputeLookupHash(rawNationalId);
        if (hash is null)
            return;

        var exists = await db.EmployeeSensitiveData.AnyAsync(
            x => x.NationalIdHash == hash
                 && (!excludeEmployeeId.HasValue || x.EmployeeId != excludeEmployeeId),
            ct);

        if (exists)
            throw new ConflictException("Bu T.C. kimlik numarası başka bir personelde kayıtlı.");
    }

    public static async Task EnsureLookupsExistAsync(
        AppDbContext db,
        Guid? unitId,
        Guid? facilityId,
        Guid? employmentTypeId,
        Guid? jobTitleId,
        Guid? managerEmployeeId,
        CancellationToken ct)
    {
        if (unitId.HasValue && !await db.OrganizationUnits.AnyAsync(x => x.Id == unitId && x.Type != OrganizationUnitType.Facility, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.UnitId), "Seçilen birim bulunamadı.");

        if (facilityId.HasValue && !await db.OrganizationUnits.AnyAsync(x => x.Id == facilityId && x.Type == OrganizationUnitType.Facility, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.FacilityId), "Seçilen tesis bulunamadı.");

        if (employmentTypeId.HasValue && !await db.EmploymentTypes.AnyAsync(x => x.Id == employmentTypeId, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.EmploymentTypeId), "İstihdam türü bulunamadı.");

        if (jobTitleId.HasValue && !await db.JobTitles.AnyAsync(x => x.Id == jobTitleId, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.JobTitleId), "Unvan bulunamadı.");

        if (managerEmployeeId.HasValue && !await db.Employees.AnyAsync(x => x.Id == managerEmployeeId, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.ManagerEmployeeId), "Yönetici personel bulunamadı.");
    }

    public static async Task EnsurePrimaryDutyExistsAsync(
        AppDbContext db,
        Guid? primaryJobDutyId,
        CancellationToken ct)
    {
        if (primaryJobDutyId.HasValue && !await db.JobDuties.AnyAsync(x => x.Id == primaryJobDutyId, ct))
            throw new AppValidationException(nameof(CreateEmployeeRequest.PrimaryJobDutyId), "Seçilen fiili görev bulunamadı.");
    }

    /// <summary>Personelin birincil (fiili) görev atamasını istenen göreve göre eşitler.</summary>
    public static void SyncPrimaryAssignment(
        AppDbContext db,
        Employee employee,
        Guid? primaryJobDutyId,
        DateOnly? startDate,
        string actor)
    {
        var current = employee.Assignments
            .Where(a => a.IsPrimary && a.EndDate == null)
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        if (!primaryJobDutyId.HasValue)
        {
            if (current is not null)
            {
                current.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
                current.IsPrimary = false;
                current.UpdatedAtUtc = DateTime.UtcNow;
                current.UpdatedBy = actor;
            }
            return;
        }

        if (current is not null && current.JobDutyId == primaryJobDutyId.Value)
            return;

        // Aynı görev zaten aktif ek görevse: yeni satır açma, onu ana göreve yükselt
        var existingSameDuty = employee.Assignments
            .Where(a => a.JobDutyId == primaryJobDutyId.Value && a.EndDate == null)
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        if (current is not null)
        {
            current.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
            current.IsPrimary = false;
            current.UpdatedAtUtc = DateTime.UtcNow;
            current.UpdatedBy = actor;
        }

        if (existingSameDuty is not null)
        {
            existingSameDuty.IsPrimary = true;
            existingSameDuty.UpdatedAtUtc = DateTime.UtcNow;
            existingSameDuty.UpdatedBy = actor;
            return;
        }

        var created = new EmployeeAssignment
        {
            EmployeeId = employee.Id,
            JobDutyId = primaryJobDutyId.Value,
            IsPrimary = true,
            StartDate = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedBy = actor
        };
        employee.Assignments.Add(created);
        // Client-side Guid + yalnızca navigation.Add → DetectChanges entity'yi Modified sayıp
        // olmayan satırı UPDATE eder (DbUpdateConcurrencyException). db.Add zorunlu.
        db.EmployeeAssignments.Add(created);
    }

    public static byte CalculateProfileCompletion(Employee e, string? nationalId)
    {
        var checks = new (bool Ok, int Weight)[]
        {
            (!string.IsNullOrWhiteSpace(e.FirstName) && !string.IsNullOrWhiteSpace(e.LastName), 15),
            (!string.IsNullOrWhiteSpace(e.EmployeeNumber), 10),
            (e.BirthDate.HasValue, 5),
            (e.Gender != Gender.Unspecified, 5),
            (!string.IsNullOrWhiteSpace(e.PersonalPhone) || !string.IsNullOrWhiteSpace(e.CorporatePhone), 10),
            (!string.IsNullOrWhiteSpace(e.CorporateEmail) || !string.IsNullOrWhiteSpace(e.PersonalEmail), 10),
            (!string.IsNullOrWhiteSpace(e.Address), 5),
            (e.UnitId.HasValue, 15),
            (e.EmploymentTypeId.HasValue, 10),
            (e.JobTitleId.HasValue, 10),
            (e.HireDate.HasValue, 5),
            (!string.IsNullOrWhiteSpace(nationalId), 10)
        };

        var totalWeight = checks.Sum(c => c.Weight);
        var earned = checks.Where(c => c.Ok).Sum(c => c.Weight);
        return (byte)Math.Clamp((int)Math.Round(100.0 * earned / totalWeight), 0, 100);
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string DigitsOnly(string value) =>
        new string(value.Where(char.IsDigit).ToArray());
}
