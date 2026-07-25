using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Movements;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class GetMovementFormOptionsHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMovementFormOptionsQuery, MovementFormOptionsDto>
{
    public async Task<MovementFormOptionsDto> Handle(GetMovementFormOptionsQuery request, CancellationToken ct)
    {
        MovementMapping.EnsureView(currentUser);
        var units = await db.OrganizationUnits.AsNoTracking().Where(x => x.Type != OrganizationUnitType.Facility)
            .OrderBy(x => x.Name).Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code }).ToListAsync(ct);
        var facilities = await db.OrganizationUnits.AsNoTracking().Where(x => x.Type == OrganizationUnitType.Facility)
            .OrderBy(x => x.Name).Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code }).ToListAsync(ct);
        var titles = await db.JobTitles.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code }).ToListAsync(ct);
        var duties = await db.JobDuties.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new DutyLookupDto { Id = x.Id, Name = x.Name, CategoryLabel = x.Category.ToString() }).ToListAsync(ct);
        return new MovementFormOptionsDto
        {
            MovementTypes = MovementLabels.AllOptions()
                .Select(x => new EnumOptionDto { Value = x.Value, Label = x.Label })
                .ToList(),
            Units = units, Facilities = facilities, JobTitles = titles, Duties = duties
        };
    }
}

public sealed class CreateMovementHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateMovementCommand, Guid>
{
    public async Task<Guid> Handle(CreateMovementCommand request, CancellationToken ct)
    {
        MovementMapping.EnsureCreate(currentUser);
        await MovementMapping.EnsureEmployeeAndLookupsAsync(db, request.EmployeeId, request, ct);
        var entity = new EmployeeMovement { EmployeeId = request.EmployeeId, CreatedBy = currentUser.UserName ?? "system" };
        MovementMapping.Apply(entity, request);
        db.EmployeeMovements.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public sealed class UpdateMovementHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateMovementCommand>
{
    public async Task Handle(UpdateMovementCommand request, CancellationToken ct)
    {
        MovementMapping.EnsureCreate(currentUser);
        await MovementMapping.EnsureLookupsAsync(db, request, ct);
        var entity = await MovementMapping.FindOwnedAsync(db, request.EmployeeId, request.MovementId, ct);
        MovementMapping.Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteMovementHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteMovementCommand>
{
    public async Task Handle(DeleteMovementCommand request, CancellationToken ct)
    {
        MovementMapping.EnsureCreate(currentUser);
        var entity = await MovementMapping.FindOwnedAsync(db, request.EmployeeId, request.MovementId, ct);
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName ?? "system";
        await db.SaveChangesAsync(ct);
    }
}

internal static class MovementMapping
{
    public static void EnsureView(ICurrentUserService currentUser)
    {
        if (!currentUser.HasPermission(PermissionCodes.MovementsView))
            throw new ForbiddenException("Hareket geçmişini görüntülemek için Movements.View gerekir.");
    }

    public static void EnsureCreate(ICurrentUserService currentUser)
    {
        if (!currentUser.HasPermission(PermissionCodes.MovementsCreate))
            throw new ForbiddenException("Hareket kaydı yönetmek için Movements.Create gerekir.");
    }

    public static async Task EnsureEmployeeAndLookupsAsync(AppDbContext db, Guid employeeId, CreateMovementRequest request, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException("Personel bulunamadı.");
        await EnsureLookupsAsync(db, request, ct);
    }

    public static async Task EnsureLookupsAsync(AppDbContext db, CreateMovementRequest request, CancellationToken ct)
    {
        async Task CheckUnit(Guid? id, string field, bool facility)
        {
            if (!id.HasValue) return;
            var exists = facility
                ? await db.OrganizationUnits.AnyAsync(x => x.Id == id && x.Type == OrganizationUnitType.Facility, ct)
                : await db.OrganizationUnits.AnyAsync(x => x.Id == id && x.Type != OrganizationUnitType.Facility, ct);
            if (!exists) throw new AppValidationException(field, "Seçilen birim/tesis bulunamadı.");
        }
        await CheckUnit(request.OldUnitId, nameof(request.OldUnitId), false);
        await CheckUnit(request.NewUnitId, nameof(request.NewUnitId), false);
        await CheckUnit(request.OldFacilityId, nameof(request.OldFacilityId), true);
        await CheckUnit(request.NewFacilityId, nameof(request.NewFacilityId), true);
        if (request.OldJobTitleId.HasValue && !await db.JobTitles.AnyAsync(x => x.Id == request.OldJobTitleId, ct))
            throw new AppValidationException(nameof(request.OldJobTitleId), "Eski unvan bulunamadı.");
        if (request.NewJobTitleId.HasValue && !await db.JobTitles.AnyAsync(x => x.Id == request.NewJobTitleId, ct))
            throw new AppValidationException(nameof(request.NewJobTitleId), "Yeni unvan bulunamadı.");
        if (request.OldJobDutyId.HasValue && !await db.JobDuties.AnyAsync(x => x.Id == request.OldJobDutyId, ct))
            throw new AppValidationException(nameof(request.OldJobDutyId), "Eski görev bulunamadı.");
        if (request.NewJobDutyId.HasValue && !await db.JobDuties.AnyAsync(x => x.Id == request.NewJobDutyId, ct))
            throw new AppValidationException(nameof(request.NewJobDutyId), "Yeni görev bulunamadı.");
    }

    public static async Task<EmployeeMovement> FindOwnedAsync(AppDbContext db, Guid employeeId, Guid movementId, CancellationToken ct) =>
        await db.EmployeeMovements.FirstOrDefaultAsync(x => x.Id == movementId && x.EmployeeId == employeeId, ct)
        ?? throw new NotFoundException("Hareket kaydı bulunamadı.");

    public static void Apply(EmployeeMovement entity, CreateMovementRequest request)
    {
        entity.MovementType = request.MovementType;
        entity.OldUnitId = request.OldUnitId; entity.NewUnitId = request.NewUnitId;
        entity.OldFacilityId = request.OldFacilityId; entity.NewFacilityId = request.NewFacilityId;
        entity.OldJobTitleId = request.OldJobTitleId; entity.NewJobTitleId = request.NewJobTitleId;
        entity.OldJobDutyId = request.OldJobDutyId; entity.NewJobDutyId = request.NewJobDutyId;
        entity.StartDate = request.StartDate; entity.EndDate = request.EndDate;
        entity.Reason = Normalize(request.Reason); entity.Description = Normalize(request.Description);
        entity.ApprovedBy = Normalize(request.ApprovedBy);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
