using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Account;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Identity;

/// <summary>
/// Hesabım — kullanıcının kendi bilgilerini yönetmesi.
/// Yetki gerekmez; yalnızca kendi kaydına dokunur.
/// </summary>
public sealed class GetMyAccountHandler : IRequestHandler<GetMyAccountQuery, MyAccountDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyAccountHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MyAccountDto> Handle(GetMyAccountQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("Oturum bulunamadı.");

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        return new MyAccountDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            LastLoginAtUtc = user.LastLoginAtUtc,
            RoleNames = user.UserRoles
                .Select(x => x.Role.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList(),
            EmployeeName = user.Employee is null
                ? null
                : $"{user.Employee.FirstName} {user.Employee.LastName}".Trim()
        };
    }
}

public sealed class UpdateMyAccountHandler : IRequestHandler<UpdateMyAccountCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateMyAccountHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateMyAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("Oturum bulunamadı.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        var userName = request.UserName.Trim();
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(x => x.UserName == userName && x.Id != userId, cancellationToken))
            throw new ConflictException($"'{userName}' kullanıcı adı zaten kullanılıyor.");

        if (await _db.Users.AnyAsync(x => x.Email == email && x.Id != userId, cancellationToken))
            throw new ConflictException("Bu e-posta başka bir kullanıcıda kayıtlı.");

        user.UserName = userName;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.UpdatedAtUtc = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserName ?? user.UserName;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ChangeMyPasswordHandler : IRequestHandler<ChangeMyPasswordCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public ChangeMyPasswordHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("Oturum bulunamadı.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            throw new ValidationException("currentPassword", "Mevcut şifre hatalı.");

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        user.UpdatedBy = user.UserName;

        // Şifre değişti: açık tüm oturumlar düşsün
        await AuthService.RevokeAllRefreshTokensAsync(_db, user.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
