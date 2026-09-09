using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

internal static class EmployeeLifecycleSync
{
    public static async Task ClearManagedUnitsAsync(
        AppDbContext db,
        Guid employeeId,
        string actor,
        CancellationToken cancellationToken)
    {
        var units = await db.OrganizationUnits
            .Where(x => x.ManagerEmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var unit in units)
        {
            unit.ManagerEmployeeId = null;
            unit.UpdatedAtUtc = now;
            unit.UpdatedBy = actor;
        }
    }

    public static async Task OnStatusChangedAsync(
        AppDbContext db,
        Employee employee,
        string actor,
        CancellationToken cancellationToken)
    {
        if (!EmployeeStatusLabels.IsDeparture(employee.Status))
            return;

        await ClearManagedUnitsAsync(db, employee.Id, actor, cancellationToken);
    }

    public static async Task ApplyMovementAsync(
        AppDbContext db,
        Employee employee,
        CreateMovementRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        switch (request.MovementType)
        {
            case MovementType.UnitChange:
            case MovementType.PermanentAssignment:
                if (request.NewUnitId.HasValue)
                {
                    employee.UnitId = request.NewUnitId;
                    employee.UnitStartDate = request.StartDate;
                }

                if (request.NewFacilityId.HasValue)
                    employee.FacilityId = request.NewFacilityId;
                break;

            case MovementType.FacilityChange:
                if (request.NewFacilityId.HasValue)
                    employee.FacilityId = request.NewFacilityId;
                break;

            case MovementType.TitleChange:
                if (request.NewJobTitleId.HasValue)
                    employee.JobTitleId = request.NewJobTitleId;
                break;

            case MovementType.TransferToOtherDirectorate:
                employee.Status = EmployeeStatus.TransferredToOtherDirectorate;
                await ClearManagedUnitsAsync(db, employee.Id, actor, cancellationToken);
                break;
        }

        employee.UpdatedAtUtc = DateTime.UtcNow;
        employee.UpdatedBy = actor;
    }
}
