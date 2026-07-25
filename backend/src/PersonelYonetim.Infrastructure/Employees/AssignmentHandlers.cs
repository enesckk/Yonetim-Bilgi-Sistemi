using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class GetAssignmentFormOptionsHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAssignmentFormOptionsQuery, AssignmentFormOptionsDto>
{
    public async Task<AssignmentFormOptionsDto> Handle(GetAssignmentFormOptionsQuery request, CancellationToken ct)
    {
        AssignmentMapping.EnsureManage(currentUser);
        var duties = await db.JobDuties.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(x => new DutyLookupDto { Id = x.Id, Name = x.Name, CategoryLabel = AssignmentMapping.CategoryLabel(x.Category) })
            .ToListAsync(ct);
        return new AssignmentFormOptionsDto { Duties = duties };
    }
}

public sealed class CreateAssignmentHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IUserNotificationService notifications)
    : IRequestHandler<CreateAssignmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateAssignmentCommand request, CancellationToken ct)
    {
        AssignmentMapping.EnsureManage(currentUser);
        await AssignmentMapping.EnsureEmployeeAndDutyAsync(db, request.EmployeeId, request.JobDutyId, ct);
        await AssignmentMapping.EnsureNoOverlappingDutyAsync(
            db, request.EmployeeId, request.JobDutyId, request.StartDate, request.EndDate, excludeId: null, ct);
        var actor = currentUser.UserName ?? "system";
        var entity = new EmployeeAssignment { EmployeeId = request.EmployeeId, CreatedBy = actor };
        AssignmentMapping.Apply(entity, request);
        if (AssignmentMapping.IsActivePrimary(entity))
            await AssignmentMapping.DemoteOtherActivePrimariesAsync(db, request.EmployeeId, null, actor, ct);
        db.EmployeeAssignments.Add(entity);
        AssignmentMapping.AddDutyMovement(db, request.EmployeeId,
            request.IsPrimary ? MovementType.DutyChange : MovementType.AdditionalDutyAssigned,
            null, request.JobDutyId, request.StartDate,
            request.IsPrimary ? "Ana görev ataması oluşturuldu." : "Ek görev ataması oluşturuldu.", actor);
        await db.SaveChangesAsync(ct);

        var emp = await db.Employees.AsNoTracking()
            .Where(x => x.Id == request.EmployeeId)
            .Select(x => x.FirstName + " " + x.LastName)
            .FirstOrDefaultAsync(ct) ?? "Personel";
        var duty = await db.JobDuties.AsNoTracking()
            .Where(x => x.Id == request.JobDutyId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct) ?? "görev";

        await notifications.NotifyUsersWithPermissionAsync(
            PermissionCodes.EmployeesView,
            "Personelin görevi değiştirildi",
            $"{emp} için {(request.IsPrimary ? "ana" : "ek")} görev atandı: {duty}.",
            NotificationSeverity.Info,
            Application.Features.Notifications.NotificationCategories.Assignments,
            $"/employees/{request.EmployeeId}",
            ct);

        return entity.Id;
    }
}

public sealed class UpdateAssignmentHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IUserNotificationService notifications)
    : IRequestHandler<UpdateAssignmentCommand>
{
    public async Task Handle(UpdateAssignmentCommand request, CancellationToken ct)
    {
        AssignmentMapping.EnsureManage(currentUser);
        await AssignmentMapping.EnsureDutyExistsAsync(db, request.JobDutyId, ct);
        var entity = await AssignmentMapping.FindOwnedAsync(db, request.EmployeeId, request.AssignmentId, ct);
        await AssignmentMapping.EnsureNoOverlappingDutyAsync(
            db, request.EmployeeId, request.JobDutyId, request.StartDate, request.EndDate,
            excludeId: request.AssignmentId, ct);
        var actor = currentUser.UserName ?? "system";
        var oldDutyId = entity.JobDutyId;
        var wasEnded = entity.EndDate.HasValue;
        AssignmentMapping.Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = actor;
        if (AssignmentMapping.IsActivePrimary(entity))
            await AssignmentMapping.DemoteOtherActivePrimariesAsync(db, request.EmployeeId, request.AssignmentId, actor, ct);
        if (oldDutyId != request.JobDutyId)
            AssignmentMapping.AddDutyMovement(db, request.EmployeeId, MovementType.DutyChange, oldDutyId, request.JobDutyId,
                request.StartDate, "Görev ataması güncellendi (görev değişti).", actor);
        else if (!wasEnded && request.EndDate.HasValue)
            AssignmentMapping.AddDutyMovement(db, request.EmployeeId, MovementType.DutyRemoved, oldDutyId, null,
                request.EndDate.Value, "Görev ataması sonlandırıldı.", actor);
        await db.SaveChangesAsync(ct);

        if (oldDutyId != request.JobDutyId || (!wasEnded && request.EndDate.HasValue))
        {
            var emp = await db.Employees.AsNoTracking()
                .Where(x => x.Id == request.EmployeeId)
                .Select(x => x.FirstName + " " + x.LastName)
                .FirstOrDefaultAsync(ct) ?? "Personel";

            await notifications.NotifyUsersWithPermissionAsync(
                PermissionCodes.EmployeesView,
                "Personelin görevi değiştirildi",
                $"{emp} görev kaydı güncellendi.",
                NotificationSeverity.Info,
                Application.Features.Notifications.NotificationCategories.Assignments,
                $"/employees/{request.EmployeeId}",
                ct);
        }
    }
}

public sealed class DeleteAssignmentHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteAssignmentCommand>
{
    public async Task Handle(DeleteAssignmentCommand request, CancellationToken ct)
    {
        AssignmentMapping.EnsureManage(currentUser);
        var entity = await AssignmentMapping.FindOwnedAsync(db, request.EmployeeId, request.AssignmentId, ct);
        var actor = currentUser.UserName ?? "system";
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = actor;
        AssignmentMapping.AddDutyMovement(db, request.EmployeeId, MovementType.DutyRemoved, entity.JobDutyId, null,
            DateOnly.FromDateTime(DateTime.Today), "Görev ataması silindi.", actor);
        await db.SaveChangesAsync(ct);
    }
}

internal static class AssignmentMapping
{
    public static void EnsureManage(ICurrentUserService currentUser)
    {
        if (!currentUser.HasPermission(PermissionCodes.AssignmentsManage))
            throw new ForbiddenException("Görev ataması yönetimi için Assignments.Manage gerekir.");
    }

    public static async Task EnsureEmployeeAndDutyAsync(AppDbContext db, Guid employeeId, Guid dutyId, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException("Personel bulunamadı.");
        await EnsureDutyExistsAsync(db, dutyId, ct);
    }

    public static async Task EnsureDutyExistsAsync(AppDbContext db, Guid dutyId, CancellationToken ct)
    {
        if (!await db.JobDuties.AnyAsync(x => x.Id == dutyId && x.IsActive, ct))
            throw new AppValidationException(nameof(UpsertAssignmentRequest.JobDutyId), "Seçilen görev bulunamadı veya pasif.");
    }

    /// <summary>
    /// Aynı personelde aynı görev tanımı için tarih aralığı örtüşen ikinci atamayı engeller.
    /// (Ana + ek olarak aynı aktif görevin iki kez görünmesini önler.)
    /// </summary>
    public static async Task EnsureNoOverlappingDutyAsync(
        AppDbContext db,
        Guid employeeId,
        Guid jobDutyId,
        DateOnly startDate,
        DateOnly? endDate,
        Guid? excludeId,
        CancellationToken ct)
    {
        var others = await db.EmployeeAssignments
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId
                        && x.JobDutyId == jobDutyId
                        && (!excludeId.HasValue || x.Id != excludeId))
            .Select(x => new { x.Id, x.StartDate, x.EndDate, x.IsPrimary })
            .ToListAsync(ct);

        var conflict = others.FirstOrDefault(x =>
            RangesOverlap(startDate, endDate, x.StartDate, x.EndDate));

        if (conflict is null)
            return;

        var status = conflict.EndDate is null
            ? (conflict.IsPrimary ? "aktif ana görev" : "aktif ek görev")
            : "tarih aralığı örtüşen bir atama";

        throw new AppValidationException(
            nameof(UpsertAssignmentRequest.JobDutyId),
            $"Bu görev için zaten {status} var. Aynı görevi iki kez ekleyemezsiniz; mevcut kaydı düzenleyin veya sonlandırın.");
    }

    private static bool RangesOverlap(DateOnly aStart, DateOnly? aEnd, DateOnly bStart, DateOnly? bEnd)
    {
        var aTo = aEnd ?? DateOnly.MaxValue;
        var bTo = bEnd ?? DateOnly.MaxValue;
        return aStart <= bTo && bStart <= aTo;
    }

    public static async Task<EmployeeAssignment> FindOwnedAsync(AppDbContext db, Guid employeeId, Guid assignmentId, CancellationToken ct) =>
        await db.EmployeeAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.EmployeeId == employeeId, ct)
        ?? throw new NotFoundException("Görev ataması bulunamadı.");

    public static async Task DemoteOtherActivePrimariesAsync(AppDbContext db, Guid employeeId, Guid? excludeId, string actor, CancellationToken ct)
    {
        var others = await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId && x.IsPrimary && x.EndDate == null &&
            (!excludeId.HasValue || x.Id != excludeId)).ToListAsync(ct);
        foreach (var item in others)
        {
            item.IsPrimary = false;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.UpdatedBy = actor;
        }
    }

    public static bool IsActivePrimary(EmployeeAssignment entity) => entity.IsPrimary && entity.EndDate is null;

    public static void AddDutyMovement(AppDbContext db, Guid employeeId, MovementType type, Guid? oldDutyId, Guid? newDutyId,
        DateOnly startDate, string description, string actor) =>
        db.EmployeeMovements.Add(new EmployeeMovement
        {
            EmployeeId = employeeId, MovementType = type, OldJobDutyId = oldDutyId, NewJobDutyId = newDutyId,
            StartDate = startDate, Description = description, CreatedBy = actor
        });

    public static void Apply(EmployeeAssignment entity, UpsertAssignmentRequest request)
    {
        entity.JobDutyId = request.JobDutyId;
        entity.IsPrimary = request.IsPrimary;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Description = Normalize(request.Description);
    }

    public static string CategoryLabel(DutyCategory category) => category switch
    {
        DutyCategory.Manager => "Yönetici", DutyCategory.Administrative => "İdari", DutyCategory.Instructor => "Eğitmen",
        DutyCategory.Technical => "Teknik", DutyCategory.Reception => "Danışma", DutyCategory.Library => "Kütüphane",
        DutyCategory.Auxiliary => "Yardımcı", DutyCategory.Cleaning => "Temizlik", DutyCategory.Project => "Proje",
        DutyCategory.SocialMedia => "Sosyal medya", DutyCategory.PublicRelations => "Halkla ilişkiler", _ => "Diğer"
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
