using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class UploadEmployeePhotoHandler : IRequestHandler<UploadEmployeePhotoCommand>
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public UploadEmployeePhotoHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task Handle(UploadEmployeePhotoCommand request, CancellationToken cancellationToken)
    {
        EnsureUpdateAndUpload();

        var ext = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExtensions.Contains(ext))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Yalnızca JPG veya PNG fotoğraf yüklenebilir."]
            });
        }

        var employee = await EmployeePhotoAccess.FindWritableAsync(
            _db, _currentUser, request.EmployeeId, cancellationToken);

        var saved = await _files.SaveAsync(
            request.Content,
            request.OriginalFileName,
            request.ContentType,
            $"employee-photos/{request.EmployeeId:N}",
            cancellationToken);

        var previous = employee.PhotoPath;
        employee.PhotoPath = saved.RelativePath;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previous)
            && !string.Equals(previous, saved.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            await _files.DeleteIfExistsAsync(previous, cancellationToken);
        }
    }

    private void EnsureUpdateAndUpload()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesUpdate))
            throw new ForbiddenException("Fotoğraf yüklemek için Employees.Update gerekir.");
        if (!_currentUser.HasPermission(PermissionCodes.FilesUpload))
            throw new ForbiddenException("Dosya yükleme yetkiniz yok (Files.Upload).");
    }
}

public sealed class DeleteEmployeePhotoHandler : IRequestHandler<DeleteEmployeePhotoCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public DeleteEmployeePhotoHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task Handle(DeleteEmployeePhotoCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesUpdate))
            throw new ForbiddenException("Fotoğraf silmek için Employees.Update gerekir.");
        if (!_currentUser.HasPermission(PermissionCodes.FilesUpload))
            throw new ForbiddenException("Dosya kaldırma için Files.Upload gerekir.");

        var employee = await EmployeePhotoAccess.FindWritableAsync(
            _db, _currentUser, request.EmployeeId, cancellationToken);

        await _files.DeleteIfExistsAsync(employee.PhotoPath, cancellationToken);
        employee.PhotoPath = null;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class OpenEmployeePhotoHandler : IRequestHandler<OpenEmployeePhotoQuery, EmployeePhotoDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly ISensitiveAccessLogger _sensitiveAccess;

    public OpenEmployeePhotoHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        ISensitiveAccessLogger sensitiveAccess)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _sensitiveAccess = sensitiveAccess;
    }

    public async Task<EmployeePhotoDto> Handle(
        OpenEmployeePhotoQuery request,
        CancellationToken cancellationToken)
    {
        var selfEmployeeId = _currentUser.UserId is null
            ? null
            : await _db.Users.AsNoTracking()
                .Where(x => x.Id == _currentUser.UserId)
                .Select(x => x.EmployeeId)
                .FirstOrDefaultAsync(cancellationToken);

        var isSelf = selfEmployeeId.HasValue && selfEmployeeId.Value == request.EmployeeId;

        if (!isSelf && !_currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Fotoğraf görmek için Employees.View gerekir.");

        Domain.Entities.Employee employee;
        if (isSelf)
        {
            employee = await _db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.EmployeeId, cancellationToken)
                ?? throw new NotFoundException("Personel bulunamadı veya bu kayda erişim yetkiniz yok.");
        }
        else
        {
            employee = await EmployeePhotoAccess.FindReadableAsync(
                _db, _currentUser, request.EmployeeId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(employee.PhotoPath))
            throw new NotFoundException("Bu personele ait fotoğraf yok.");

        var opened = await _files.OpenReadAsync(employee.PhotoPath, cancellationToken)
            ?? throw new NotFoundException("Fotoğraf dosyası bulunamadı.");

        if (!isSelf)
        {
            await _sensitiveAccess.LogAsync(
                SensitiveAccessActions.ViewEmployeePhoto,
                "Employee",
                request.EmployeeId.ToString(),
                new { kind = "photo" },
                cancellationToken);
        }

        return new EmployeePhotoDto
        {
            Stream = opened.Stream,
            ContentType = opened.ContentType,
            DownloadFileName = opened.DownloadFileName
        };
    }
}

internal static class EmployeePhotoAccess
{
    public static async Task<Domain.Entities.Employee> FindReadableAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var query = db.Employees.AsNoTracking().Where(x => x.Id == employeeId);
        query = await ApplyUnitScopeAsync(db, currentUser, query, cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı veya bu kayda erişim yetkiniz yok.");

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı veya bu kayda erişim yetkiniz yok.");
    }

    public static async Task<Domain.Entities.Employee> FindWritableAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var query = db.Employees.Where(x => x.Id == employeeId);
        query = await ApplyUnitScopeAsync(db, currentUser, query, cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı veya bu kayda erişim yetkiniz yok.");

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı veya bu kayda erişim yetkiniz yok.");
    }

    private static async Task<IQueryable<Domain.Entities.Employee>?> ApplyUnitScopeAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        IQueryable<Domain.Entities.Employee> employees,
        CancellationToken cancellationToken)
    {
        if (currentUser.HasPermission(PermissionCodes.EmployeesViewAllUnits))
            return employees;

        if (currentUser.UserId is null)
            return null;

        var unitId = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUser.UserId)
            .Select(x => x.Employee != null ? x.Employee.UnitId : null)
            .FirstOrDefaultAsync(cancellationToken);

        if (unitId is null)
            return null;

        return employees.Where(x => x.UnitId == unitId);
    }
}
