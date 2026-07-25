using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class GetSpecialConditionFormOptionsHandler
    : IRequestHandler<GetSpecialConditionFormOptionsQuery, SpecialConditionFormOptionsDto>
{
    private readonly ICurrentUserService _currentUser;

    public GetSpecialConditionFormOptionsHandler(ICurrentUserService currentUser) =>
        _currentUser = currentUser;

    public Task<SpecialConditionFormOptionsDto> Handle(
        GetSpecialConditionFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureManage();
        return Task.FromResult(new SpecialConditionFormOptionsDto
        {
            SuggestedTypes =
            [
                "Engellilik",
                "Kronik hastalık",
                "Gebelik / doğum",
                "Bakım yükümlülüğü",
                "Adli / idari süreç",
                "İş sağlığı kısıtı",
                "Diğer"
            ]
        });
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum yönetimi için Employees.ManageSpecialConditions gerekir.");
    }
}

public sealed class CreateSpecialConditionHandler
    : IRequestHandler<CreateSpecialConditionCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateSpecialConditionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateSpecialConditionCommand request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await SpecialConditionMapping.EnsureEmployeeExistsAsync(_db, request.EmployeeId, cancellationToken);

        var entity = new SpecialCondition
        {
            EmployeeId = request.EmployeeId,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        SpecialConditionMapping.Apply(entity, request);
        _db.SpecialConditions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum eklemek için Employees.ManageSpecialConditions gerekir.");
    }
}

public sealed class UpdateSpecialConditionHandler : IRequestHandler<UpdateSpecialConditionCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateSpecialConditionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateSpecialConditionCommand request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var entity = await SpecialConditionMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.ConditionId, cancellationToken);
        SpecialConditionMapping.Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum güncellemek için Employees.ManageSpecialConditions gerekir.");
    }
}

public sealed class DeleteSpecialConditionHandler : IRequestHandler<DeleteSpecialConditionCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public DeleteSpecialConditionHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task Handle(DeleteSpecialConditionCommand request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var entity = await SpecialConditionMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.ConditionId, cancellationToken);

        await _files.DeleteIfExistsAsync(entity.DocumentPath, cancellationToken);
        entity.DocumentPath = null;
        entity.HasDocument = false;
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum silmek için Employees.ManageSpecialConditions gerekir.");
    }
}

public sealed class UploadSpecialConditionDocumentHandler
    : IRequestHandler<UploadSpecialConditionDocumentCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public UploadSpecialConditionDocumentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task Handle(UploadSpecialConditionDocumentCommand request, CancellationToken cancellationToken)
    {
        EnsureManageAndUpload();
        var entity = await SpecialConditionMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.ConditionId, cancellationToken);
        var saved = await _files.SaveAsync(
            request.Content,
            request.OriginalFileName,
            request.ContentType,
            $"special-conditions/{request.EmployeeId:N}",
            cancellationToken);

        var previous = entity.DocumentPath;
        entity.DocumentPath = saved.RelativePath;
        entity.HasDocument = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previous)
            && !string.Equals(previous, saved.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            await _files.DeleteIfExistsAsync(previous, cancellationToken);
        }
    }

    private void EnsureManageAndUpload()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum belgesi için Employees.ManageSpecialConditions gerekir.");
        if (!_currentUser.HasPermission(PermissionCodes.FilesUpload))
            throw new ForbiddenException("Dosya yükleme yetkiniz yok (Files.Upload).");
    }
}

public sealed class RemoveSpecialConditionDocumentHandler
    : IRequestHandler<RemoveSpecialConditionDocumentCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;

    public RemoveSpecialConditionDocumentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task Handle(RemoveSpecialConditionDocumentCommand request, CancellationToken cancellationToken)
    {
        EnsureManageAndUpload();
        var entity = await SpecialConditionMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.ConditionId, cancellationToken);
        await _files.DeleteIfExistsAsync(entity.DocumentPath, cancellationToken);
        entity.DocumentPath = null;
        entity.HasDocument = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureManageAndUpload()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesManageSpecialConditions))
            throw new ForbiddenException("Özel durum belgesi için Employees.ManageSpecialConditions gerekir.");
        if (!_currentUser.HasPermission(PermissionCodes.FilesUpload))
            throw new ForbiddenException("Dosya kaldırma için Files.Upload gerekir.");
    }
}

public sealed class OpenSpecialConditionDocumentHandler
    : IRequestHandler<OpenSpecialConditionDocumentQuery, SpecialConditionDocumentDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly ISensitiveAccessLogger _sensitiveAccess;

    public OpenSpecialConditionDocumentHandler(
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

    public async Task<SpecialConditionDocumentDto> Handle(
        OpenSpecialConditionDocumentQuery request,
        CancellationToken cancellationToken)
    {
        EnsureViewAndFilesView();
        var entity = await SpecialConditionMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.ConditionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(entity.DocumentPath))
            throw new NotFoundException("Bu kayda ait belge yok.");

        var opened = await _files.OpenReadAsync(entity.DocumentPath, cancellationToken)
            ?? throw new NotFoundException("Belge dosyası bulunamadı.");

        await _sensitiveAccess.LogAsync(
            SensitiveAccessActions.ViewSpecialConditionDocument,
            "SpecialCondition",
            entity.Id.ToString(),
            new { employeeId = request.EmployeeId, conditionType = entity.ConditionType },
            cancellationToken);

        return new SpecialConditionDocumentDto
        {
            Stream = opened.Stream,
            ContentType = opened.ContentType,
            DownloadFileName = SpecialConditionMapping.SanitizeDownloadName(
                entity.ConditionType, Path.GetExtension(entity.DocumentPath))
        };
    }

    private void EnsureViewAndFilesView()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EmployeesViewSpecialConditions))
            throw new ForbiddenException("Özel durum belgesi için ViewSpecialConditions gerekir.");
        if (!_currentUser.HasPermission(PermissionCodes.FilesView))
            throw new ForbiddenException("Dosya görüntüleme yetkiniz yok (Files.View).");
    }
}

internal static class SpecialConditionMapping
{
    public static async Task EnsureEmployeeExistsAsync(AppDbContext db, Guid employeeId, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException("Personel bulunamadı.");
    }

    public static async Task<SpecialCondition> FindOwnedAsync(
        AppDbContext db,
        Guid employeeId,
        Guid conditionId,
        CancellationToken ct)
    {
        var entity = await db.SpecialConditions
            .FirstOrDefaultAsync(x => x.Id == conditionId && x.EmployeeId == employeeId, ct);
        return entity ?? throw new NotFoundException("Özel durum kaydı bulunamadı.");
    }

    public static void Apply(SpecialCondition entity, UpsertSpecialConditionRequest request)
    {
        entity.ConditionType = request.ConditionType.Trim();
        entity.Description = request.Description.Trim();
        entity.StartDate = request.StartDate;
        entity.IsPermanent = request.IsPermanent;
        entity.EndDate = request.IsPermanent ? null : request.EndDate;
        entity.RequiresDutyAdjustment = request.RequiresDutyAdjustment;
        entity.RequiresWorkspaceAdjustment = request.RequiresWorkspaceAdjustment;
    }

    public static string SanitizeDownloadName(string conditionType, string? extension)
    {
        var baseName = string.Join(
            "_",
            conditionType.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "ozel-durum";
        if (baseName.Length > 80)
            baseName = baseName[..80];
        return baseName + (extension ?? string.Empty);
    }
}
