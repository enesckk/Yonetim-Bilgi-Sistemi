using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Settlements;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Settlements;

internal static class SettlementWriteAccess
{
    public static void RequireManage(ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Mahalle kaydını güncelleme yetkiniz yok.");
    }

    public static string? Clean(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= max ? t : t[..max];
    }
}

public sealed class UpdateSettlementProfileHandler : IRequestHandler<UpdateSettlementProfileCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IMemoryCache _cache;

    public UpdateSettlementProfileHandler(AppDbContext db, ICurrentUserService current, IMemoryCache cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    public async Task Handle(UpdateSettlementProfileCommand request, CancellationToken cancellationToken)
    {
        SettlementWriteAccess.RequireManage(_current);
        var row = await _db.Settlements.FirstOrDefaultAsync(x => x.Id == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Yerleşim bulunamadı.");

        row.HeadmanName = SettlementWriteAccess.Clean(request.HeadmanName, 120);
        row.HeadmanPhone = SettlementWriteAccess.Clean(request.HeadmanPhone, 30);
        row.UpdatedBy = _current.UserName;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
        _cache.Remove(AppCache.SettlementsAll);
    }
}

public sealed class UpsertSettlementSchoolHandler : IRequestHandler<UpsertSettlementSchoolCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IMemoryCache _cache;

    public UpsertSettlementSchoolHandler(AppDbContext db, ICurrentUserService current, IMemoryCache cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    public async Task<Guid> Handle(UpsertSettlementSchoolCommand request, CancellationToken cancellationToken)
    {
        SettlementWriteAccess.RequireManage(_current);
        var settlement = await _db.Settlements.FirstOrDefaultAsync(x => x.Id == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Yerleşim bulunamadı.");

        var name = request.Name.Trim();
        var type = SettlementCatalog.NormalizeSchoolType(request.SchoolType);
        var clash = await _db.SettlementSchools.AnyAsync(
            x => x.SettlementId == settlement.Id
                 && x.Name == name
                 && (request.Id == null || x.Id != request.Id),
            cancellationToken);
        if (clash)
            throw new ValidationException("name", "Bu mahallede aynı adlı okul zaten var.");

        SettlementSchool row;
        if (request.Id is Guid id)
        {
            row = await _db.SettlementSchools.FirstOrDefaultAsync(
                    x => x.Id == id && x.SettlementId == settlement.Id, cancellationToken)
                ?? throw new NotFoundException("Okul bulunamadı.");
            row.UpdatedBy = _current.UserName;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            row = new SettlementSchool
            {
                SettlementId = settlement.Id,
                CreatedBy = _current.UserName
            };
            _db.SettlementSchools.Add(row);
        }

        row.Name = name;
        row.SchoolType = type;
        row.StudentCount = request.StudentCount;
        row.PrincipalName = SettlementWriteAccess.Clean(request.PrincipalName, 120);
        row.PrincipalPhone = SettlementWriteAccess.Clean(request.PrincipalPhone, 30);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
        return row.Id;
    }
}

public sealed class DeleteSettlementSchoolHandler : IRequestHandler<DeleteSettlementSchoolCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IMemoryCache _cache;

    public DeleteSettlementSchoolHandler(AppDbContext db, ICurrentUserService current, IMemoryCache cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    public async Task Handle(DeleteSettlementSchoolCommand request, CancellationToken cancellationToken)
    {
        SettlementWriteAccess.RequireManage(_current);
        var row = await _db.SettlementSchools.FirstOrDefaultAsync(
                x => x.Id == request.SchoolId && x.SettlementId == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Okul bulunamadı.");

        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        row.DeletedBy = _current.UserName;
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
    }
}

public sealed class UpsertSettlementAreaHandler : IRequestHandler<UpsertSettlementAreaCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IMemoryCache _cache;

    public UpsertSettlementAreaHandler(AppDbContext db, ICurrentUserService current, IMemoryCache cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    public async Task<Guid> Handle(UpsertSettlementAreaCommand request, CancellationToken cancellationToken)
    {
        SettlementWriteAccess.RequireManage(_current);
        var settlement = await _db.Settlements.FirstOrDefaultAsync(x => x.Id == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Yerleşim bulunamadı.");

        var name = request.Name.Trim();
        var type = SettlementCatalog.NormalizeAreaType(request.AreaType);
        var clash = await _db.SettlementAreas.AnyAsync(
            x => x.SettlementId == settlement.Id
                 && x.Name == name
                 && (request.Id == null || x.Id != request.Id),
            cancellationToken);
        if (clash)
            throw new ValidationException("name", "Bu mahallede aynı adlı alan zaten var.");

        SettlementArea row;
        if (request.Id is Guid id)
        {
            row = await _db.SettlementAreas.FirstOrDefaultAsync(
                    x => x.Id == id && x.SettlementId == settlement.Id, cancellationToken)
                ?? throw new NotFoundException("Alan bulunamadı.");
            row.UpdatedBy = _current.UserName;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            row = new SettlementArea
            {
                SettlementId = settlement.Id,
                CreatedBy = _current.UserName
            };
            _db.SettlementAreas.Add(row);
        }

        row.Name = name;
        row.AreaType = type;
        row.Note = SettlementWriteAccess.Clean(request.Note, 300);
        row.Address = SettlementWriteAccess.Clean(request.Address, 300);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
        return row.Id;
    }
}

public sealed class DeleteSettlementAreaHandler : IRequestHandler<DeleteSettlementAreaCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IMemoryCache _cache;

    public DeleteSettlementAreaHandler(AppDbContext db, ICurrentUserService current, IMemoryCache cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    public async Task Handle(DeleteSettlementAreaCommand request, CancellationToken cancellationToken)
    {
        SettlementWriteAccess.RequireManage(_current);
        var row = await _db.SettlementAreas.FirstOrDefaultAsync(
                x => x.Id == request.AreaId && x.SettlementId == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Alan bulunamadı.");

        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        row.DeletedBy = _current.UserName;
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
    }
}
