using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Stock;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Stock;

internal static class CustodyData
{
    public static IQueryable<StockCustody> Rows(AppDbContext db) => db.StockCustodies
        .IgnoreQueryFilters().Where(x => !x.IsDeleted)
        .Include(x => x.StockItem).Include(x => x.Employee).Include(x => x.Location);

    public static StockCustodyDto Map(StockCustody x) => new()
    {
        Id = x.Id, StockItemId = x.StockItemId, ItemName = x.StockItem.Name,
        ItemCode = x.StockItem.Code, UnitLabel = StockLabels.Unit(x.Unit),
        LocationId = x.LocationId, LocationName = x.Location.Name,
        EmployeeId = x.EmployeeId, EmployeeName = x.Employee.FullName,
        EmployeeNumber = x.Employee.EmployeeNumber, Quantity = x.Quantity,
        SerialNumber = x.SerialNumber, AssignedOn = x.AssignedOn, ReturnedOn = x.ReturnedOn,
        Notes = x.Notes, ReturnNotes = x.ReturnNotes, ReplacesId = x.ReplacesId
    };

    public static async Task EnsureLocationAsync(AppDbContext db, ICurrentUserService user, Guid id, CancellationToken ct)
    {
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (allowed is not null && !allowed.Contains(id))
            throw new ForbiddenException("Bu stok yerine erişim yetkiniz yok.");
    }

    public static async Task<decimal> AssignedAsync(AppDbContext db, Guid itemId, Guid locationId, CancellationToken ct) =>
        await db.StockCustodies.Where(x => x.StockItemId == itemId && x.LocationId == locationId && x.ReturnedOn == null)
            .SumAsync(x => (decimal?)x.Quantity, ct) ?? 0;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class GetCustodyEmployeesHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<GetCustodyEmployeesQuery, IReadOnlyList<CustodyEmployeeDto>>
{
    public async Task<IReadOnlyList<CustodyEmployeeDto>> Handle(GetCustodyEmployeesQuery request, CancellationToken ct)
    {
        StockAccess.EnsureManage(user);
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        var q = db.Employees.AsNoTracking().Where(x => x.Status == EmployeeStatus.Active);
        if (allowed is not null)
            q = q.Where(x => (x.UnitId != null && allowed.Contains(x.UnitId.Value)) ||
                (x.FacilityId != null && allowed.Contains(x.FacilityId.Value)));
        return await q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new CustodyEmployeeDto(x.Id, x.FirstName + " " + x.LastName, x.EmployeeNumber)).ToListAsync(ct);
    }
}

public sealed class GetStockCustodiesHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<GetStockCustodiesQuery, IReadOnlyList<StockCustodyDto>>
{
    public async Task<IReadOnlyList<StockCustodyDto>> Handle(GetStockCustodiesQuery request, CancellationToken ct)
    {
        StockAccess.EnsureView(user);
        var q = CustodyData.Rows(db).AsNoTracking();
        var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
        if (allowed is not null) q = q.Where(x => allowed.Contains(x.LocationId));
        if (request.EmployeeId.HasValue) q = q.Where(x => x.EmployeeId == request.EmployeeId.Value);
        if (!request.IncludeReturned) q = q.Where(x => x.ReturnedOn == null);
        return (await q.OrderByDescending(x => x.AssignedOn).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(ct))
            .Select(CustodyData.Map).ToList();
    }
}

public sealed class AssignStockCustodyHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<AssignStockCustodyCommand, StockCustodyDto>
{
    public async Task<StockCustodyDto> Handle(AssignStockCustodyCommand request, CancellationToken ct)
    {
        StockAccess.EnsureManage(user);
        return await StockTransaction.RunAsync(db, async () =>
        {
            await CustodyData.EnsureLocationAsync(db, user, request.LocationId, ct);
            var serial = CustodyData.Clean(request.SerialNumber)?.ToUpperInvariant();
            var notes = CustodyData.Clean(request.Notes);
            var previousRequest = await CustodyData.Rows(db).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (previousRequest is not null)
            {
                if (previousRequest.StockItemId != request.StockItemId || previousRequest.LocationId != request.LocationId ||
                    previousRequest.EmployeeId != request.EmployeeId || previousRequest.Quantity != request.Quantity ||
                    previousRequest.SerialNumber != serial || previousRequest.AssignedOn != request.AssignedOn ||
                    previousRequest.Notes != notes || previousRequest.ReplacesId != request.ReplacesId)
                    throw new ConflictException("İşlem kimliği farklı bir zimmet için kullanılmış. Formu yeniden açın.");
                return CustodyData.Map(previousRequest);
            }

            var item = await db.StockItems.FirstOrDefaultAsync(x => x.Id == request.StockItemId && x.IsActive, ct)
                ?? throw new NotFoundException("Aktif malzeme bulunamadı.");
            if (item.Kind != StockItemKind.FixedAsset)
                throw new ConflictException("Yalnızca demirbaş türündeki malzemeler zimmetlenebilir. Malzeme türünü kontrol edin.");
            var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == request.EmployeeId && x.Status == EmployeeStatus.Active, ct)
                ?? throw new NotFoundException("Aktif personel bulunamadı.");
            var allowed = await UnitScopeHelper.AllowedUnitIdsAsync(db, user, ct);
            if (!UnitScopeHelper.IsUnitAllowed(allowed, employee.UnitId, employee.FacilityId))
                throw new ForbiddenException("Bu personele zimmet atama yetkiniz yok.");
            var location = await (await StockAccess.StockableUnitsForAsync(db, user, ct))
                .FirstOrDefaultAsync(x => x.Id == request.LocationId, ct)
                ?? throw new NotFoundException("Geçerli stok yeri bulunamadı.");

            StockCustody? old = null;
            if (request.ReplacesId.HasValue)
            {
                old = await CustodyData.Rows(db).FirstOrDefaultAsync(x => x.Id == request.ReplacesId.Value, ct)
                    ?? throw new NotFoundException("Değiştirilecek zimmet bulunamadı.");
                await CustodyData.EnsureLocationAsync(db, user, old.LocationId, ct);
                if (old.ReturnedOn.HasValue) throw new ConflictException("Bu zimmet zaten kapanmış. Listeyi yenileyin.");
                if (request.AssignedOn < old.AssignedOn)
                    throw new ConflictException("Değişim tarihi eski zimmet tarihinden önce olamaz.");
                old.ReturnedOn = request.AssignedOn;
                old.ReturnNotes = old.EmployeeId != employee.Id
                    ? "Personel devri: yeni personel için zimmet açıldı."
                    : "Yeni zimmet ile değiştirildi.";
                old.UpdatedAtUtc = DateTime.UtcNow;
                old.UpdatedBy = user.UserName;
                // Persist inside the transaction so availability and unique serial checks exclude the old custody.
                await db.SaveChangesAsync(ct);
            }

            var balance = await db.StockBalances.FirstOrDefaultAsync(x => x.StockItemId == item.Id && x.LocationId == location.Id, ct);
            var assigned = await CustodyData.AssignedAsync(db, item.Id, location.Id, ct);
            if (request.Quantity > (balance?.Quantity ?? 0) - assigned)
                throw new ConflictException("Bu yerde yeterli kullanılabilir stok yok. Zimmetli miktarlar tekrar verilemez.");
            if (serial is not null && await db.StockCustodies.AnyAsync(x => x.StockItemId == item.Id && x.SerialNumber == serial && x.ReturnedOn == null, ct))
                throw new ConflictException("Bu seri numarası zaten aktif bir zimmette kullanılıyor.");

            var row = new StockCustody
            {
                Id = request.Id, StockItemId = item.Id, StockItem = item, LocationId = location.Id, Location = location,
                EmployeeId = employee.Id, Employee = employee, Quantity = request.Quantity, Unit = item.Unit, SerialNumber = serial,
                AssignedOn = request.AssignedOn, Notes = notes, ReplacesId = old?.Id, CreatedBy = user.UserName
            };
            db.StockCustodies.Add(row);
            await db.SaveChangesAsync(ct);
            return CustodyData.Map(row);
        }, ct);
    }
}

public sealed class ReturnStockCustodyHandler(AppDbContext db, ICurrentUserService user)
    : IRequestHandler<ReturnStockCustodyCommand, StockCustodyDto>
{
    public async Task<StockCustodyDto> Handle(ReturnStockCustodyCommand request, CancellationToken ct)
    {
        StockAccess.EnsureManage(user);
        return await StockTransaction.RunAsync(db, async () =>
        {
            var row = await CustodyData.Rows(db).FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                ?? throw new NotFoundException("Zimmet bulunamadı.");
            await CustodyData.EnsureLocationAsync(db, user, row.LocationId, ct);
            if (row.ReturnedOn.HasValue)
            {
                if (row.ReturnedOn != request.ReturnedOn || row.ReturnNotes != CustodyData.Clean(request.Notes))
                    throw new ConflictException("Bu zimmet zaten kapatılmış. Listeyi yenileyin.");
                return CustodyData.Map(row);
            }
            if (request.ReturnedOn < row.AssignedOn)
                throw new ConflictException("İade tarihi zimmet tarihinden önce olamaz.");
            row.ReturnedOn = request.ReturnedOn;
            row.ReturnNotes = CustodyData.Clean(request.Notes);
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedBy = user.UserName;
            await db.SaveChangesAsync(ct);
            return CustodyData.Map(row);
        }, ct);
    }
}
