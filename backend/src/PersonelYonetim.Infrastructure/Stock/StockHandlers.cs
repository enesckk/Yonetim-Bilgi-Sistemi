using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Stock;

internal static class StockAccess
{
    public static void EnsureView(ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.StockView)
            && !user.HasPermission(PermissionCodes.StockManage))
            throw new ForbiddenException("Stok görüntüleme yetkiniz yok.");
    }

    public static void EnsureManage(ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.StockManage))
            throw new ForbiddenException("Stok yönetme yetkiniz yok.");
    }

    public static string LocationType(OrganizationUnitType t) => t switch
    {
        OrganizationUnitType.Municipality => "Belediye",
        OrganizationUnitType.DeputyPresidency => "Başkan Yardımcılığı",
        OrganizationUnitType.Directorate => "Müdürlük",
        OrganizationUnitType.MainUnit => "Ana birim",
        OrganizationUnitType.SubUnit => "Alt birim",
        OrganizationUnitType.Facility => "Tesis",
        _ => t.ToString()
    };

    public static IQueryable<OrganizationUnit> StockableUnits(AppDbContext db) =>
        db.OrganizationUnits.Where(x =>
            x.Type == OrganizationUnitType.Facility
            && x.Status != OrganizationUnitStatus.OutOfUse);

    public static async Task<IQueryable<OrganizationUnit>> StockableUnitsForAsync(
        AppDbContext db,
        ICurrentUserService user,
        CancellationToken ct)
    {
        var q = StockableUnits(db);
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (allowed is null)
            return q;
        return q.Where(x => allowed.Contains(x.Id));
    }

    public static bool IsLow(decimal quantity, decimal min) => min > 0 && quantity <= min;
}

public sealed class GetStockOptionsHandler : IRequestHandler<GetStockOptionsQuery, StockOptionsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockOptionsHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockOptionsDto> Handle(GetStockOptionsQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);
        var units = await (await StockAccess.StockableUnitsForAsync(_db, _user, ct))
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return new StockOptionsDto
        {
            Categories = StockLabels.CategoryOptions().Select(x => new StockLookupDto { Value = x.Value, Label = x.Label }).ToList(),
            Units = StockLabels.UnitOptions().Select(x => new StockLookupDto { Value = x.Value, Label = x.Label }).ToList(),
            MovementTypes = StockLabels.MovementOptions().Select(x => new StockLookupDto { Value = x.Value, Label = x.Label }).ToList(),
            Locations = units.Select(x => new StockLocationOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                TypeLabel = StockAccess.LocationType(x.Type)
            }).ToList()
        };
    }
}

public sealed class GetStockSummaryHandler : IRequestHandler<GetStockSummaryQuery, StockSummaryDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockSummaryHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockSummaryDto> Handle(GetStockSummaryQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);

        var catalogCount = await _db.StockItems.CountAsync(x => x.IsActive, ct);
        var locations = await (await StockAccess.StockableUnitsForAsync(_db, _user, ct)).AsNoTracking().ToListAsync(ct);
        var locationIds = locations.Select(x => x.Id).ToHashSet();
        var balances = await _db.StockBalances
            .AsNoTracking()
            .Include(x => x.StockItem)
            .Include(x => x.Location)
            .Where(x => x.StockItem != null && x.StockItem.IsActive && locationIds.Contains(x.LocationId))
            .ToListAsync(ct);

        var cards = locations
            .Select(loc =>
            {
                var lines = balances.Where(b => b.LocationId == loc.Id).ToList();
                return new StockLocationCardDto
                {
                    Id = loc.Id,
                    Name = loc.Name,
                    Code = loc.Code,
                    TypeLabel = StockAccess.LocationType(loc.Type),
                    ItemCount = lines.Count(x => x.Quantity > 0),
                    TotalQuantity = lines.Sum(x => x.Quantity),
                    LowCount = lines.Count(x => x.StockItem != null && StockAccess.IsLow(x.Quantity, x.StockItem.MinQuantity))
                };
            })
            .OrderByDescending(x => x.ItemCount)
            .ThenBy(x => x.Name)
            .ToList();

        var low = balances
            .Where(x => x.StockItem != null && StockAccess.IsLow(x.Quantity, x.StockItem.MinQuantity))
            .OrderBy(x => x.Quantity)
            .Take(12)
            .Select(x => new StockLowLineDto
            {
                StockItemId = x.StockItemId,
                LocationId = x.LocationId,
                ItemName = x.StockItem!.Name,
                LocationName = x.Location?.Name ?? "—",
                UnitLabel = StockLabels.Unit(x.StockItem.Unit),
                Quantity = x.Quantity,
                MinQuantity = x.StockItem.MinQuantity
            })
            .ToList();

        var recent = await StockMapper.RecentAsync(_db, null, null, 8, ct);

        return new StockSummaryDto
        {
            CatalogCount = catalogCount,
            LocationCount = locations.Count,
            LocationsWithStock = cards.Count(x => x.ItemCount > 0),
            LowCount = balances.Count(x => x.StockItem != null && StockAccess.IsLow(x.Quantity, x.StockItem.MinQuantity)),
            Locations = cards,
            LowStock = low,
            RecentMovements = recent
        };
    }
}

public sealed class GetStockItemsHandler : IRequestHandler<GetStockItemsQuery, IReadOnlyList<StockItemListRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockItemsHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IReadOnlyList<StockItemListRowDto>> Handle(GetStockItemsQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);
        var q = _db.StockItems.AsNoTracking().AsQueryable();
        if (!request.IncludeInactive)
            q = q.Where(x => x.IsActive);
        if (request.Category.HasValue)
            q = q.Where(x => x.Category == request.Category.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(x =>
                x.Name.Contains(term)
                || (x.Code != null && x.Code.Contains(term))
                || (x.Brand != null && x.Brand.Contains(term)));
        }

        var items = await q.OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync(ct);
        var itemIds = items.Select(x => x.Id).ToList();
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(_db, _user, ct);
        var balancesQ = _db.StockBalances.AsNoTracking().Where(x => itemIds.Contains(x.StockItemId));
        if (allowed is not null)
            balancesQ = balancesQ.Where(x => allowed.Contains(x.LocationId));
        var balances = await balancesQ
            .Select(x => new
            {
                x.StockItemId,
                x.Quantity,
                x.LocationId,
                LocationName = x.Location != null ? x.Location.Name : ""
            })
            .ToListAsync(ct);
        var grouped = balances
            .GroupBy(x => x.StockItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return items.Select(item =>
        {
            grouped.TryGetValue(item.Id, out var lines);
            lines ??= [];
            var stocked = lines
                .Where(x => x.Quantity > 0)
                .OrderBy(x => x.LocationName)
                .ToList();
            var qty = stocked.Sum(x => x.Quantity);
            var isLow = lines.Any(b => StockAccess.IsLow(b.Quantity, item.MinQuantity));
            return new StockItemListRowDto
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Category = item.Category,
                CategoryLabel = StockLabels.Category(item.Category),
                Unit = item.Unit,
                UnitLabel = StockLabels.Unit(item.Unit),
                Brand = item.Brand,
                Model = item.Model,
                MinQuantity = item.MinQuantity,
                TotalQuantity = qty,
                LocationCount = stocked.Count,
                Locations = stocked.Select(x => new StockItemLocationBriefDto
                {
                    LocationId = x.LocationId,
                    LocationName = string.IsNullOrWhiteSpace(x.LocationName) ? "—" : x.LocationName,
                    Quantity = x.Quantity
                }).ToList(),
                IsLow = isLow,
                IsActive = item.IsActive
            };
        }).ToList();
    }
}

public sealed class GetStockItemHandler : IRequestHandler<GetStockItemQuery, StockItemDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockItemHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockItemDetailDto> Handle(GetStockItemQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);
        var item = await _db.StockItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Malzeme bulunamadı.");
        var balances = await _db.StockBalances.AsNoTracking()
            .Include(x => x.Location)
            .Where(x => x.StockItemId == item.Id)
            .OrderBy(x => x.Location!.Name)
            .ToListAsync(ct);

        return StockMapper.Detail(item, balances);
    }
}

public sealed class GetStockLocationHandler : IRequestHandler<GetStockLocationQuery, StockLocationDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockLocationHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockLocationDetailDto> Handle(GetStockLocationQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);
        var loc = await (await StockAccess.StockableUnitsForAsync(_db, _user, ct)).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.LocationId, ct)
            ?? throw new NotFoundException("Stok yeri bulunamadı.");

        var lines = await _db.StockBalances.AsNoTracking()
            .Include(x => x.StockItem)
            .Where(x => x.LocationId == loc.Id && x.StockItem != null && x.StockItem.IsActive)
            .OrderBy(x => x.StockItem!.Category)
            .ThenBy(x => x.StockItem!.Name)
            .ToListAsync(ct);

        var mapped = lines.Select(x => new StockLocationLineDto
        {
            StockItemId = x.StockItemId,
            Name = x.StockItem!.Name,
            Code = x.StockItem.Code,
            Category = x.StockItem.Category,
            CategoryLabel = StockLabels.Category(x.StockItem.Category),
            Unit = x.StockItem.Unit,
            UnitLabel = StockLabels.Unit(x.StockItem.Unit),
            Brand = x.StockItem.Brand,
            Model = x.StockItem.Model,
            Quantity = x.Quantity,
            MinQuantity = x.StockItem.MinQuantity,
            IsLow = StockAccess.IsLow(x.Quantity, x.StockItem.MinQuantity)
        }).ToList();

        return new StockLocationDetailDto
        {
            Id = loc.Id,
            Name = loc.Name,
            Code = loc.Code,
            TypeLabel = StockAccess.LocationType(loc.Type),
            ItemCount = mapped.Count(x => x.Quantity > 0),
            LowCount = mapped.Count(x => x.IsLow),
            Lines = mapped
        };
    }
}

public sealed class GetStockMovementsHandler : IRequestHandler<GetStockMovementsQuery, IReadOnlyList<StockMovementDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public GetStockMovementsHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IReadOnlyList<StockMovementDto>> Handle(GetStockMovementsQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(_user);
        var take = Math.Clamp(request.Take, 1, 200);
        return await StockMapper.RecentAsync(_db, request.StockItemId, request.LocationId, take, ct);
    }
}

public sealed class UpsertStockItemHandler : IRequestHandler<UpsertStockItemCommand, StockItemDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public UpsertStockItemHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockItemDetailDto> Handle(UpsertStockItemCommand request, CancellationToken ct)
    {
        StockAccess.EnsureManage(_user);
        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();

        if (code is not null)
        {
            var codeTaken = await _db.StockItems.AnyAsync(
                x => x.Code == code && (!request.Id.HasValue || x.Id != request.Id.Value),
                ct);
            if (codeTaken)
                throw new ConflictException("Bu stok kodu zaten kullanılıyor.");
        }

        StockItem item;
        if (request.Id.HasValue)
        {
            item = await _db.StockItems.FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new NotFoundException("Malzeme bulunamadı.");
        }
        else
        {
            item = new StockItem();
            _db.StockItems.Add(item);
        }

        item.Name = name;
        item.Code = code;
        item.Category = request.Category;
        item.Unit = request.Unit;
        item.Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        item.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.MinQuantity = request.MinQuantity;
        item.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);

        var balances = await _db.StockBalances.AsNoTracking()
            .Include(x => x.Location)
            .Where(x => x.StockItemId == item.Id)
            .ToListAsync(ct);
        return StockMapper.Detail(item, balances);
    }
}

public sealed class ArchiveStockItemHandler : IRequestHandler<ArchiveStockItemCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public ArchiveStockItemHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task Handle(ArchiveStockItemCommand request, CancellationToken ct)
    {
        StockAccess.EnsureManage(_user);
        var item = await _db.StockItems.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Malzeme bulunamadı.");
        item.IsActive = false;
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        item.DeletedBy = _user.UserName;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class CreateStockMovementHandler : IRequestHandler<CreateStockMovementCommand, StockMovementDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public CreateStockMovementHandler(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<StockMovementDto> Handle(CreateStockMovementCommand request, CancellationToken ct)
    {
        StockAccess.EnsureManage(_user);
        var item = await _db.StockItems.FirstOrDefaultAsync(x => x.Id == request.StockItemId && x.IsActive, ct)
            ?? throw new NotFoundException("Malzeme bulunamadı.");

        var occurred = request.OccurredOn ?? DateOnly.FromDateTime(DateTime.Today);
        var qty = decimal.Round(request.Quantity, 2);

        switch (request.MovementType)
        {
            case StockMovementType.Inbound:
                await AddAsync(item.Id, request.ToLocationId!.Value, qty, ct);
                break;
            case StockMovementType.Outbound:
                await SubtractAsync(item.Id, request.FromLocationId!.Value, qty, ct);
                break;
            case StockMovementType.Transfer:
                await SubtractAsync(item.Id, request.FromLocationId!.Value, qty, ct);
                await AddAsync(item.Id, request.ToLocationId!.Value, qty, ct);
                break;
            case StockMovementType.Adjustment:
                await SetAsync(item.Id, request.ToLocationId!.Value, qty, ct);
                break;
            default:
                throw new ValidationException("movementType", "Hareket türü geçersiz.");
        }

        var movement = new StockMovement
        {
            StockItemId = item.Id,
            MovementType = request.MovementType,
            FromLocationId = request.MovementType is StockMovementType.Inbound or StockMovementType.Adjustment
                ? null
                : request.FromLocationId,
            ToLocationId = request.MovementType == StockMovementType.Outbound ? null : request.ToLocationId,
            Quantity = qty,
            OccurredOn = occurred,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        _db.StockMovements.Add(movement);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(movement).Reference(x => x.StockItem).LoadAsync(ct);
        if (movement.FromLocationId.HasValue)
            await _db.Entry(movement).Reference(x => x.FromLocation).LoadAsync(ct);
        if (movement.ToLocationId.HasValue)
            await _db.Entry(movement).Reference(x => x.ToLocation).LoadAsync(ct);

        return new StockMovementDto
        {
            Id = movement.Id,
            StockItemId = movement.StockItemId,
            ItemName = movement.StockItem?.Name ?? item.Name,
            ItemCode = movement.StockItem?.Code ?? item.Code,
            UnitLabel = StockLabels.Unit(item.Unit),
            MovementType = movement.MovementType,
            MovementTypeLabel = StockLabels.Movement(movement.MovementType),
            FromLocationId = movement.FromLocationId,
            FromLocationName = movement.FromLocation?.Name,
            ToLocationId = movement.ToLocationId,
            ToLocationName = movement.ToLocation?.Name,
            Quantity = movement.Quantity,
            OccurredOn = movement.OccurredOn,
            Reason = movement.Reason,
            CreatedAtUtc = movement.CreatedAtUtc
        };
    }

    private async Task EnsureLocationAsync(Guid locationId, CancellationToken ct)
    {
        var exists = await (await StockAccess.StockableUnitsForAsync(_db, _user, ct)).AnyAsync(x => x.Id == locationId, ct);
        if (!exists)
            throw new ValidationException("locationId", "Geçerli bir stok yeri seçin.");
    }

    private async Task<StockBalance> GetOrCreateAsync(Guid itemId, Guid locationId, CancellationToken ct)
    {
        await EnsureLocationAsync(locationId, ct);
        var bal = await _db.StockBalances.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.StockItemId == itemId && x.LocationId == locationId, ct);
        if (bal is null)
        {
            bal = new StockBalance { StockItemId = itemId, LocationId = locationId, Quantity = 0 };
            _db.StockBalances.Add(bal);
            return bal;
        }

        if (bal.IsDeleted)
        {
            bal.IsDeleted = false;
            bal.DeletedAtUtc = null;
            bal.DeletedBy = null;
            bal.Quantity = 0;
        }

        return bal;
    }

    private async Task AddAsync(Guid itemId, Guid locationId, decimal qty, CancellationToken ct)
    {
        var bal = await GetOrCreateAsync(itemId, locationId, ct);
        bal.Quantity += qty;
    }

    private async Task SubtractAsync(Guid itemId, Guid locationId, decimal qty, CancellationToken ct)
    {
        var bal = await GetOrCreateAsync(itemId, locationId, ct);
        if (bal.Quantity < qty)
            throw new ValidationException("quantity", "Bu yerde yeterli stok yok.");
        bal.Quantity -= qty;
    }

    private async Task SetAsync(Guid itemId, Guid locationId, decimal qty, CancellationToken ct)
    {
        var bal = await GetOrCreateAsync(itemId, locationId, ct);
        bal.Quantity = qty;
    }
}

internal static class StockMapper
{
    public static StockItemDetailDto Detail(StockItem item, IReadOnlyList<StockBalance> balances)
    {
        var lines = balances
            .OrderBy(x => x.Location?.Name)
            .Select(x => new StockBalanceLineDto
            {
                LocationId = x.LocationId,
                LocationName = x.Location?.Name ?? "—",
                LocationTypeLabel = x.Location is null ? "" : StockAccess.LocationType(x.Location.Type),
                Quantity = x.Quantity,
                MinQuantity = item.MinQuantity,
                IsLow = StockAccess.IsLow(x.Quantity, item.MinQuantity),
                Notes = x.Notes
            })
            .ToList();

        return new StockItemDetailDto
        {
            Id = item.Id,
            Name = item.Name,
            Code = item.Code,
            Category = item.Category,
            CategoryLabel = StockLabels.Category(item.Category),
            Unit = item.Unit,
            UnitLabel = StockLabels.Unit(item.Unit),
            Brand = item.Brand,
            Model = item.Model,
            Description = item.Description,
            MinQuantity = item.MinQuantity,
            TotalQuantity = lines.Sum(x => x.Quantity),
            IsActive = item.IsActive,
            Balances = lines
        };
    }

    public static async Task<IReadOnlyList<StockMovementDto>> RecentAsync(
        AppDbContext db,
        Guid? itemId,
        Guid? locationId,
        int take,
        CancellationToken ct)
    {
        var q = db.StockMovements.AsNoTracking()
            .Include(x => x.StockItem)
            .Include(x => x.FromLocation)
            .Include(x => x.ToLocation)
            .AsQueryable();
        if (itemId.HasValue)
            q = q.Where(x => x.StockItemId == itemId.Value);
        if (locationId.HasValue)
            q = q.Where(x => x.FromLocationId == locationId || x.ToLocationId == locationId);

        var rows = await q.OrderByDescending(x => x.OccurredOn)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(x => new StockMovementDto
        {
            Id = x.Id,
            StockItemId = x.StockItemId,
            ItemName = x.StockItem?.Name ?? "—",
            ItemCode = x.StockItem?.Code,
            UnitLabel = x.StockItem is null ? "" : StockLabels.Unit(x.StockItem.Unit),
            MovementType = x.MovementType,
            MovementTypeLabel = StockLabels.Movement(x.MovementType),
            FromLocationId = x.FromLocationId,
            FromLocationName = x.FromLocation?.Name,
            ToLocationId = x.ToLocationId,
            ToLocationName = x.ToLocation?.Name,
            Quantity = x.Quantity,
            OccurredOn = x.OccurredOn,
            Reason = x.Reason,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }
}
