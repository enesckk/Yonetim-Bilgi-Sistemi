using FluentValidation;
using MediatR;
using PersonelYonetim.Application.Common.Validation;

namespace PersonelYonetim.Application.Features.Admin;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserListItemDto>>;

public sealed class UserListItemDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleListItemDto>>;

public sealed class RoleListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public bool IsSystemRole { get; init; }
    public int PermissionCount { get; init; }
    public int UserCount { get; init; }
}

public sealed class CreateUserCommand : IRequest<Guid>
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public IReadOnlyList<string> RoleCodes { get; set; } = [];
}

public sealed class UpdateUserCommand : IRequest
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? EmployeeId { get; set; }
    public IReadOnlyList<string> RoleCodes { get; set; } = [];
}

public sealed class ResetUserPasswordCommand : IRequest
{
    public Guid Id { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Kullanıcı adı yalnızca harf, rakam, . _ - içerebilir.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).ApplyPasswordPolicy();
        RuleFor(x => x.RoleCodes).NotEmpty().WithMessage("En az bir rol seçilmelidir.");
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.RoleCodes).NotEmpty().WithMessage("En az bir rol seçilmelidir.");
    }
}

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewPassword).ApplyPasswordPolicy();
    }
}

/// <summary>
/// Runtime matris: DB'deki Role ← RolePermission → Permission.
/// Koddaki RolePermissionMatrix yalnızca seed şablonudur.
/// </summary>
public sealed record GetRolePermissionMatrixQuery : IRequest<RolePermissionMatrixDto>;

public sealed class RolePermissionMatrixDto
{
    public IReadOnlyList<PermissionGroupDto> PermissionGroups { get; init; } = [];
    public IReadOnlyList<RoleMatrixRowDto> Roles { get; init; } = [];
}

public sealed class PermissionGroupDto
{
    public string Group { get; init; } = string.Empty;
    public IReadOnlyList<PermissionItemDto> Items { get; init; } = [];
}

public sealed class PermissionItemDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class RoleMatrixRowDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public bool IsSystemRole { get; init; }
    /// <summary>DB'de atanmış permission kodları (JWT buradan üretilir).</summary>
    public IReadOnlyList<string> PermissionCodes { get; init; } = [];
    /// <summary>Kod şablonundaki varsayılanlar — sapmayı görmek için.</summary>
    public IReadOnlyList<string> SeedPermissionCodes { get; init; } = [];
    public bool DiffersFromSeed { get; init; }
}

public sealed class UpdateRolePermissionsCommand : IRequest
{
    public Guid RoleId { get; set; }
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}

public sealed class ResetRolePermissionsToSeedCommand : IRequest
{
    public Guid RoleId { get; set; }
}

public sealed class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionCodes).NotNull();
    }
}

public sealed class ResetRolePermissionsToSeedCommandValidator : AbstractValidator<ResetRolePermissionsToSeedCommand>
{
    public ResetRolePermissionsToSeedCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
