using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Settings;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Settings;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Settings;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly AppDbContext _db;

    public AppSettingsService(AppDbContext db) => _db = db;

    public async Task<string> GetStringAsync(
        string key,
        string fallback,
        CancellationToken cancellationToken = default)
    {
        var value = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public async Task<int> GetIntAsync(
        string key,
        int fallback,
        CancellationToken cancellationToken = default)
    {
        var raw = await GetStringAsync(key, fallback.ToString(), cancellationToken);
        return int.TryParse(raw, out var n) ? n : fallback;
    }

    public async Task<bool> GetBoolAsync(
        string key,
        bool fallback,
        CancellationToken cancellationToken = default)
    {
        var raw = await GetStringAsync(key, fallback ? "true" : "false", cancellationToken);
        return bool.TryParse(raw, out var b) ? b : fallback;
    }
}

public sealed class GetAppSettingsHandler
    : IRequestHandler<GetAppSettingsQuery, IReadOnlyList<AppSettingDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAppSettingsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AppSettingDto>> Handle(
        GetAppSettingsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureManage();

        return await _db.AppSettings
            .AsNoTracking()
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.DisplayName)
            .Select(x => new AppSettingDto
            {
                Key = x.Key,
                Value = x.Value,
                ValueType = x.ValueType,
                GroupName = x.GroupName,
                DisplayName = x.DisplayName,
                Description = x.Description,
                IsReadOnly = x.IsReadOnly
            })
            .ToListAsync(cancellationToken);
    }

    private void EnsureManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.SettingsManage))
            throw new ForbiddenException("Sistem ayarlarını yönetme yetkiniz yok.");
    }
}

public sealed class UpdateAppSettingHandler : IRequestHandler<UpdateAppSettingCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateAppSettingHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateAppSettingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SettingsManage))
            throw new ForbiddenException("Sistem ayarlarını yönetme yetkiniz yok.");

        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(x => x.Key == request.Key.Trim(), cancellationToken)
            ?? throw new NotFoundException("Ayar bulunamadı.");

        if (setting.IsReadOnly)
            throw new ConflictException("Bu ayar salt okunur; değiştirilemez.");

        var normalized = NormalizeAndValidate(setting.ValueType, request.Value);
        setting.Value = normalized;
        setting.UpdatedAtUtc = DateTime.UtcNow;
        setting.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeAndValidate(AppSettingValueType type, string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        switch (type)
        {
            case AppSettingValueType.Integer:
                if (!int.TryParse(value, out var n) || n < 1 || n > 100_000)
                    throw new ValidationException("value", "1–100000 arası tam sayı girin.");
                return n.ToString();
            case AppSettingValueType.Boolean:
                if (!bool.TryParse(value, out var b))
                    throw new ValidationException("value", "true veya false olmalıdır.");
                return b ? "true" : "false";
            default:
                if (string.IsNullOrWhiteSpace(value))
                    throw new ValidationException("value", "Değer boş olamaz.");
                if (value.Length > 2000)
                    throw new ValidationException("value", "En fazla 2000 karakter.");
                return value;
        }
    }
}
