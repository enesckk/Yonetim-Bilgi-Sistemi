using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class SetEmployeeStatusHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IUserNotificationService notifications,
    IMemoryCache cache)
    : IRequestHandler<SetEmployeeStatusCommand>
{
    public async Task Handle(SetEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionCodes.EmployeesSetStatus))
            throw new ForbiddenException("Personel durumunu değiştirmek için Employees.SetStatus gerekir.");

        var employee = await db.Employees
            .FirstOrDefaultAsync(x => x.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı.");

        if (employee.Status == request.Status)
            throw new ConflictException("Personel zaten bu durumda.");

        var actor = currentUser.UserName ?? "system";
        var previous = employee.Status;
        employee.Status = request.Status;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = actor;

        var movementType = MapMovementType(request.Status);
        if (movementType is { } type)
        {
            db.EmployeeMovements.Add(new EmployeeMovement
            {
                EmployeeId = employee.Id,
                MovementType = type,
                OldUnitId = employee.UnitId,
                NewUnitId = employee.UnitId,
                OldFacilityId = employee.FacilityId,
                NewFacilityId = employee.FacilityId,
                OldJobTitleId = employee.JobTitleId,
                NewJobTitleId = employee.JobTitleId,
                StartDate = request.EffectiveDate,
                Reason = request.Reason.Trim(),
                Description =
                    $"Durum: {EmployeeStatusLabels.For(previous)} → {EmployeeStatusLabels.For(request.Status)}",
                CreatedBy = actor
            });
        }

        await EmployeeLifecycleSync.OnStatusChangedAsync(db, employee, actor, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        cache.InvalidateOrgLookups();

        if (previous != EmployeeStatus.Passive && request.Status == EmployeeStatus.Passive)
        {
            await notifications.NotifyUsersWithPermissionAsync(
                PermissionCodes.EmployeesView,
                "Personel pasife alındı",
                $"{employee.FirstName} {employee.LastName} pasife alındı. Gerekçe: {request.Reason.Trim()}",
                NotificationSeverity.Warning,
                Application.Features.Notifications.NotificationCategories.Employees,
                $"/employees/{employee.Id}",
                cancellationToken);
        }
    }

    private static MovementType? MapMovementType(EmployeeStatus status) => status switch
    {
        EmployeeStatus.LeftJob => MovementType.LeftJob,
        EmployeeStatus.Retired => MovementType.Retirement,
        EmployeeStatus.TransferredToOtherDirectorate => MovementType.TransferToOtherDirectorate,
        EmployeeStatus.DutyEnded => MovementType.DutyRemoved,
        EmployeeStatus.Active => MovementType.ReturnToDuty,
        _ => null
    };
}

public sealed class ArchiveEmployeeHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    : IRequestHandler<ArchiveEmployeeCommand>
{
    public async Task Handle(ArchiveEmployeeCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionCodes.EmployeesArchive))
            throw new ForbiddenException("Personel silmek için Employees.Archive gerekir.");

        var employee = await db.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel bulunamadı.");

        if (employee.IsDeleted)
            throw new ConflictException("Bu kayıt zaten arşivlenmiş.");

        var actor = currentUser.UserName ?? "system";
        employee.IsDeleted = true;
        employee.DeletedAtUtc = DateTime.UtcNow;
        employee.DeletedBy = actor;
        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = actor;
        employee.Status = EmployeeStatus.Passive;

        await EmployeeLifecycleSync.ClearManagedUnitsAsync(db, employee.Id, actor, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        cache.InvalidateOrgLookups();
    }
}
