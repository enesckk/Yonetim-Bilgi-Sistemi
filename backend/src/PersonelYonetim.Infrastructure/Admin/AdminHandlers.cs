using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Admin;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Identity;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Admin;

/// <summary>
/// RBAC yönetimi: User ← UserRole → Role ← RolePermission → Permission
/// Kullanıcıya doğrudan permission verilmez; rol üzerinden gelir.
/// </summary>
public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserListItemDto>>
{
    private readonly AppDbContext _db;

    public GetUsersHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserListItemDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(x => x.Employee)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserListItemDto
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = u.Email,
            IsActive = u.IsActive,
            LastLoginAtUtc = u.LastLoginAtUtc,
            EmployeeId = u.EmployeeId,
            EmployeeName = u.Employee is null ? null : u.Employee.FullName,
            RoleCodes = u.UserRoles
                .Where(r => r.Role.Code is not null)
                .Select(r => r.Role.Code!)
                .OrderBy(c => c)
                .ToList(),
            RoleNames = u.UserRoles
                .Select(r => r.Role.Name)
                .OrderBy(n => n)
                .ToList()
        }).ToList();
    }
}

public sealed class GetRolesHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleListItemDto>>
{
    private readonly AppDbContext _db;

    public GetRolesHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleListItemDto>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new RoleListItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                IsSystemRole = x.IsSystemRole,
                PermissionCount = x.RolePermissions.Count,
                UserCount = x.UserRoles.Count
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotificationService _notifications;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public CreateUserHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IUserNotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        EnsureUsersManage();

        var userName = request.UserName.Trim();
        if (await _db.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
            throw new ConflictException($"'{userName}' kullanıcı adı zaten var.");

        if (await _db.Users.AnyAsync(x => x.Email == request.Email.Trim(), cancellationToken))
            throw new ConflictException("Bu e-posta başka bir kullanıcıda kayıtlı.");

        if (request.EmployeeId.HasValue
            && !await _db.Employees.AnyAsync(x => x.Id == request.EmployeeId, cancellationToken))
            throw new ValidationException("employeeId", "Personel bulunamadı.");

        var roles = await ResolveRolesAsync(request.RoleCodes, cancellationToken);
        var actor = _currentUser.UserName ?? "system";

        var user = new AppUser
        {
            UserName = userName,
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            EmployeeId = request.EmployeeId,
            IsActive = true,
            CreatedBy = actor
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole
            {
                RoleId = role.Id,
                CreatedBy = actor
            });
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyAsync(
            user.Id,
            "Hesabınız oluşturuldu",
            $"Merhaba {user.DisplayName}. Giriş kullanıcı adınız: {user.UserName}. İlk girişte şifrenizi değiştirmeniz önerilir.",
            NotificationSeverity.Success,
            "Security",
            "/",
            cancellationToken);

        return user.Id;
    }

    private void EnsureUsersManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.UsersManage))
            throw new ForbiddenException("Kullanıcı yönetimi için Users.Manage gerekir.");
    }

    private async Task<List<Role>> ResolveRolesAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var normalized = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
            throw new ValidationException("roleCodes", "En az bir rol seçilmelidir.");

        var roles = await _db.Roles
            .Where(r => r.Code != null && normalized.Contains(r.Code))
            .ToListAsync(ct);

        if (roles.Count != normalized.Count)
        {
            var found = roles.Select(r => r.Code!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = normalized.Where(c => !found.Contains(c));
            throw new ValidationException("roleCodes", $"Bilinmeyen rol: {string.Join(", ", missing)}");
        }

        return roles;
    }
}

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateUserHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.UsersManage))
            throw new ForbiddenException("Kullanıcı yönetimi için Users.Manage gerekir.");

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        // Kendini pasifleştirme / tüm rollerini silme tuzağı
        if (_currentUser.UserId == user.Id && !request.IsActive)
            throw new ValidationException("isActive", "Kendi hesabınızı pasifleştiremezsiniz.");

        if (await _db.Users.AnyAsync(
                x => x.Email == request.Email.Trim() && x.Id != request.Id, cancellationToken))
            throw new ConflictException("Bu e-posta başka bir kullanıcıda kayıtlı.");

        if (request.EmployeeId.HasValue
            && !await _db.Employees.AnyAsync(x => x.Id == request.EmployeeId, cancellationToken))
            throw new ValidationException("employeeId", "Personel bulunamadı.");

        var newRoles = await ResolveRolesAsync(request.RoleCodes, cancellationToken);

        // Son SYSTEM_ADMIN'i rolsüz bırakma
        var wasAdmin = user.UserRoles.Any(r => r.Role.Code == RoleCodes.SystemAdmin);
        var willBeAdmin = newRoles.Any(r => r.Code == RoleCodes.SystemAdmin);
        if (wasAdmin && !willBeAdmin)
        {
            var otherAdmins = await _db.UserRoles
                .CountAsync(
                    x => x.Role.Code == RoleCodes.SystemAdmin && x.UserId != user.Id,
                    cancellationToken);
            if (otherAdmins == 0)
                throw new ConflictException("Sistemde en az bir Sistem Yöneticisi kalmalıdır.");
        }

        var actor = _currentUser.UserName ?? "system";
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.IsActive = request.IsActive;
        user.EmployeeId = request.EmployeeId;
        user.UpdatedAtUtc = DateTime.UtcNow;
        user.UpdatedBy = actor;

        // Rolleri değiştir: soft-delete eski, ekle yeni (basit replace)
        foreach (var ur in user.UserRoles.ToList())
        {
            ur.IsDeleted = true;
            ur.DeletedAtUtc = DateTime.UtcNow;
            ur.DeletedBy = actor;
        }

        foreach (var role in newRoles)
        {
            var link = new UserRole
            {
                RoleId = role.Id,
                CreatedBy = actor
            };
            user.UserRoles.Add(link);
            _db.UserRoles.Add(link);
        }

        if (!request.IsActive)
            await AuthService.RevokeAllRefreshTokensAsync(_db, user.Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Role>> ResolveRolesAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var normalized = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var roles = await _db.Roles
            .Where(r => r.Code != null && normalized.Contains(r.Code))
            .ToListAsync(ct);

        if (roles.Count != normalized.Count)
            throw new ValidationException("roleCodes", "Geçersiz rol kodu var.");

        return roles;
    }
}

public sealed class ResetUserPasswordHandler : IRequestHandler<ResetUserPasswordCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public ResetUserPasswordHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.UsersManage))
            throw new ForbiddenException("Şifre sıfırlamak için Users.Manage gerekir.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserName ?? "system";

        await AuthService.RevokeAllRefreshTokensAsync(_db, user.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetRolePermissionMatrixHandler
    : IRequestHandler<GetRolePermissionMatrixQuery, RolePermissionMatrixDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRolePermissionMatrixHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RolePermissionMatrixDto> Handle(
        GetRolePermissionMatrixQuery request,
        CancellationToken cancellationToken)
    {
        EnsureRolesManage();

        var groups = PermissionCatalog.All
            .GroupBy(p => p.GroupName)
            .Select(g => new PermissionGroupDto
            {
                Group = g.Key,
                Items = g.Select(p => new PermissionItemDto
                {
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description
                }).ToList()
            })
            .ToList();

        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var rows = roles.Select(r =>
        {
            var assigned = r.RolePermissions
                .Select(rp => rp.Permission.Code)
                .OrderBy(c => c)
                .ToList();
            var seed = r.Code is null
                ? Array.Empty<string>()
                : RolePermissionMatrix.GetPermissionsForRole(r.Code).OrderBy(c => c).ToArray();
            var differs = !assigned.SequenceEqual(seed, StringComparer.Ordinal);

            return new RoleMatrixRowDto
            {
                Id = r.Id,
                Name = r.Name,
                Code = r.Code,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                PermissionCodes = assigned,
                SeedPermissionCodes = seed,
                DiffersFromSeed = differs
            };
        }).ToList();

        return new RolePermissionMatrixDto
        {
            PermissionGroups = groups,
            Roles = rows
        };
    }

    private void EnsureRolesManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.RolesManage))
            throw new ForbiddenException("Rol–yetki matrisi için Roles.Manage gerekir.");
    }
}

public sealed class UpdateRolePermissionsHandler : IRequestHandler<UpdateRolePermissionsCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateRolePermissionsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.RolesManage))
            throw new ForbiddenException("Rol–yetki güncellemek için Roles.Manage gerekir.");

        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Rol bulunamadı.");

        var normalized = request.PermissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var permissions = await _db.Permissions
            .Where(p => normalized.Contains(p.Code))
            .ToListAsync(cancellationToken);

        if (permissions.Count != normalized.Count)
        {
            var found = permissions.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
            var missing = normalized.Where(c => !found.Contains(c));
            throw new ValidationException("permissionCodes", $"Bilinmeyen yetki: {string.Join(", ", missing)}");
        }

        await EnsureCriticalPermissionsRemainAsync(role.Id, normalized, cancellationToken);

        var actor = _currentUser.UserName ?? "system";
        var keep = permissions.Select(p => p.Id).ToHashSet();

        foreach (var link in role.RolePermissions.ToList())
        {
            if (keep.Contains(link.PermissionId))
                continue;
            link.IsDeleted = true;
            link.DeletedAtUtc = DateTime.UtcNow;
            link.DeletedBy = actor;
        }

        var existingActive = role.RolePermissions
            .Where(rp => !rp.IsDeleted)
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        foreach (var perm in permissions)
        {
            if (existingActive.Contains(perm.Id))
                continue;
            var link = new RolePermission
            {
                PermissionId = perm.Id,
                CreatedBy = actor
            };
            role.RolePermissions.Add(link);
            _db.RolePermissions.Add(link);
        }

        role.UpdatedAtUtc = DateTime.UtcNow;
        role.UpdatedBy = actor;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sistemde en az bir rol Roles.Manage ve Users.Manage taşımalı;
    /// aksi halde kimse kullanıcı/rol yönetemez (kilitlenme).
    /// </summary>
    private async Task EnsureCriticalPermissionsRemainAsync(
        Guid roleIdBeingEdited,
        IReadOnlyList<string> newCodesForRole,
        CancellationToken ct)
    {
        foreach (var critical in new[] { PermissionCodes.RolesManage, PermissionCodes.UsersManage })
        {
            var stillHas = newCodesForRole.Contains(critical, StringComparer.Ordinal);
            if (stillHas)
                continue;

            var otherRoleHasIt = await _db.RolePermissions
                .AnyAsync(
                    rp => rp.RoleId != roleIdBeingEdited && rp.Permission.Code == critical,
                    ct);

            if (!otherRoleHasIt)
            {
                throw new ConflictException(
                    $"Sistemde en az bir rol '{critical}' yetkisini taşımalıdır. " +
                    "Bu yetkiyi tüm rollerden kaldıramazsınız.");
            }
        }
    }
}

public sealed class ResetRolePermissionsToSeedHandler
    : IRequestHandler<ResetRolePermissionsToSeedCommand>
{
    private readonly ISender _sender;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ResetRolePermissionsToSeedHandler(
        ISender sender,
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        _sender = sender;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ResetRolePermissionsToSeedCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.RolesManage))
            throw new ForbiddenException("Varsayılana dönmek için Roles.Manage gerekir.");

        var role = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Rol bulunamadı.");

        if (string.IsNullOrWhiteSpace(role.Code))
            throw new ValidationException("roleId", "Kodsuz özel rollerde seed şablonu yok.");

        var seed = RolePermissionMatrix.GetPermissionsForRole(role.Code);
        await _sender.Send(
            new UpdateRolePermissionsCommand
            {
                RoleId = role.Id,
                PermissionCodes = seed
            },
            cancellationToken);
    }
}
