using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Certificates;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Certificates;

/// <summary>
/// Sertifika kataloğu — tanımlar, süre durumu istatistikleri ve personel dağılımı.
/// </summary>
internal static class CertificateCatalogHelpers
{
    public const int ExpiringSoonDays = 90;

    public static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "Diğer" : category.Trim();

    public static (string Status, string Label) ExpiryStatus(DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is null)
            return ("open", "Süresiz");
        if (expiresOn < today)
            return ("expired", "Süresi dolmuş");
        if (expiresOn <= today.AddDays(ExpiringSoonDays))
            return ("expiring", "Süresi yaklaşıyor");
        return ("valid", "Geçerli");
    }
}

public sealed class GetCertificateCatalogHandler
    : IRequestHandler<GetCertificateCatalogQuery, CertificateCatalogDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCertificateCatalogHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CertificateCatalogDto> Handle(
        GetCertificateCatalogQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView)
            && !_currentUser.HasPermission(PermissionCodes.CertificatesManage))
        {
            throw new ForbiddenException("Sertifika kataloğunu görüntüleme yetkiniz yok.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(CertificateCatalogHelpers.ExpiringSoonDays);

        var definitions = await _db.CertificateDefinitions
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Category,
                x.IsActive,
                EmployeeCount = x.EmployeeCertificates.Count(),
                ExpiredCount = x.EmployeeCertificates.Count(c =>
                    c.ExpiresOn != null && c.ExpiresOn < today),
                ExpiringSoonCount = x.EmployeeCertificates.Count(c =>
                    c.ExpiresOn != null && c.ExpiresOn >= today && c.ExpiresOn <= soon),
                ValidCount = x.EmployeeCertificates.Count(c =>
                    c.ExpiresOn == null || c.ExpiresOn > soon)
            })
            .ToListAsync(cancellationToken);

        var employeesWithout = await _db.Employees
            .AsNoTracking()
            .CountAsync(e => !e.Certificates.Any(), cancellationToken);

        var items = definitions
            .Select(x =>
            {
                var cat = CertificateCatalogHelpers.NormalizeCategory(x.Category);
                return new CertificateCatalogItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Category = cat,
                    CategoryLabel = cat,
                    IsActive = x.IsActive,
                    EmployeeCount = x.EmployeeCount,
                    ExpiredCount = x.ExpiredCount,
                    ExpiringSoonCount = x.ExpiringSoonCount,
                    ValidCount = x.ValidCount
                };
            })
            .ToList();

        var categories = items
            .GroupBy(x => x.Category)
            .OrderBy(g => g.Key == "Diğer")
            .ThenBy(g => g.Key, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: true))
            .Select(g => new CertificateCategoryGroupDto
            {
                Category = g.Key,
                CategoryLabel = g.Key,
                Certificates = g
                    .OrderBy(i => i.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: true))
                    .ToList()
            })
            .ToList();

        return new CertificateCatalogDto
        {
            TotalDefinitions = items.Count,
            ActiveDefinitions = items.Count(x => x.IsActive),
            TotalAssignments = items.Sum(x => x.EmployeeCount),
            ExpiredCount = items.Sum(x => x.ExpiredCount),
            ExpiringSoonCount = items.Sum(x => x.ExpiringSoonCount),
            EmployeesWithoutCertificates = employeesWithout,
            Categories = categories
        };
    }
}

public sealed class GetCertificateCatalogDetailHandler
    : IRequestHandler<GetCertificateCatalogDetailQuery, CertificateCatalogDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCertificateCatalogDetailHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CertificateCatalogDetailDto?> Handle(
        GetCertificateCatalogDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView)
            && !_currentUser.HasPermission(PermissionCodes.CertificatesManage))
        {
            throw new ForbiddenException("Sertifika detayını görüntüleme yetkiniz yok.");
        }

        var def = await _db.CertificateDefinitions
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new { x.Id, x.Name, x.Category, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (def is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await _db.EmployeeCertificates
            .AsNoTracking()
            .Where(x => x.CertificateDefinitionId == request.Id)
            .Select(x => new
            {
                x.Id,
                EmployeeId = x.Employee.Id,
                x.Employee.FirstName,
                x.Employee.LastName,
                UnitName = x.Employee.Unit != null ? x.Employee.Unit.Name : null,
                JobTitleName = x.Employee.JobTitle != null ? x.Employee.JobTitle.Name : null,
                x.Issuer,
                x.IssuedOn,
                x.ExpiresOn
            })
            .ToListAsync(cancellationToken);

        var employees = rows
            .Select(x =>
            {
                var (status, label) = CertificateCatalogHelpers.ExpiryStatus(x.ExpiresOn, today);
                return new CertificateEmployeeItemDto
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    FullName = $"{x.FirstName} {x.LastName}".Trim(),
                    UnitName = x.UnitName,
                    JobTitleName = x.JobTitleName,
                    Issuer = x.Issuer,
                    IssuedOn = x.IssuedOn,
                    ExpiresOn = x.ExpiresOn,
                    ExpiryStatus = status,
                    ExpiryStatusLabel = label
                };
            })
            .OrderBy(x => x.ExpiryStatus == "expired" ? 0 : x.ExpiryStatus == "expiring" ? 1 : 2)
            .ThenBy(x => x.ExpiresOn)
            .ThenBy(x => x.FullName)
            .ToList();

        var cat = CertificateCatalogHelpers.NormalizeCategory(def.Category);

        return new CertificateCatalogDetailDto
        {
            Id = def.Id,
            Name = def.Name,
            Category = cat,
            CategoryLabel = cat,
            IsActive = def.IsActive,
            EmployeeCount = employees.Count,
            ExpiredCount = employees.Count(e => e.ExpiryStatus == "expired"),
            ExpiringSoonCount = employees.Count(e => e.ExpiryStatus == "expiring"),
            ValidCount = employees.Count(e => e.ExpiryStatus is "valid" or "open"),
            Employees = employees
        };
    }
}

public sealed class CreateCertificateDefinitionHandler
    : IRequestHandler<CreateCertificateDefinitionCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateCertificateDefinitionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        CreateCertificateDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika eklemek için Certificates.Manage gerekir.");

        var name = request.Name.Trim();
        var exists = await _db.CertificateDefinitions.AnyAsync(
            x => x.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (exists)
            throw new ConflictException("Bu isimde bir sertifika tanımı zaten var.");

        var entity = new CertificateDefinition
        {
            Name = name,
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            IsActive = true,
            CreatedBy = _currentUser.UserName ?? "system"
        };

        _db.CertificateDefinitions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed class UpdateCertificateDefinitionHandler
    : IRequestHandler<UpdateCertificateDefinitionCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCertificateDefinitionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(
        UpdateCertificateDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika güncellemek için Certificates.Manage gerekir.");

        var entity = await _db.CertificateDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Sertifika tanımı bulunamadı.");

        var name = request.Name.Trim();
        var exists = await _db.CertificateDefinitions.AnyAsync(
            x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (exists)
            throw new ConflictException("Bu isimde bir sertifika tanımı zaten var.");

        entity.Name = name;
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteCertificateDefinitionHandler
    : IRequestHandler<DeleteCertificateDefinitionCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteCertificateDefinitionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(
        DeleteCertificateDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika silmek için Certificates.Manage gerekir.");

        var entity = await _db.CertificateDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Sertifika tanımı bulunamadı.");

        var inUse = await _db.EmployeeCertificates.AnyAsync(
            x => x.CertificateDefinitionId == request.Id,
            cancellationToken);
        if (inUse)
            throw new ConflictException(
                "Bu sertifika personellere atanmış. Silmek yerine pasife alabilirsiniz.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}
