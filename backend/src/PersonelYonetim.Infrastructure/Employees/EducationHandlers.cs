using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Employees;

/// <summary>
/// Eğitim MediatR handlers — eski EducationCommandService.
/// IDOR: FindOwnedAsync(employeeId, educationId) — yanlış parent → 404.
/// </summary>
public sealed class GetEducationFormOptionsHandler
    : IRequestHandler<GetEducationFormOptionsQuery, EducationFormOptionsDto>
{
    private readonly ICurrentUserService _currentUser;

    public GetEducationFormOptionsHandler(ICurrentUserService currentUser) =>
        _currentUser = currentUser;

    public Task<EducationFormOptionsDto> Handle(
        GetEducationFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureEducationManage();

        var options = new EducationFormOptionsDto
        {
            Levels =
            [
                new EnumOptionDto { Value = (int)EducationLevel.Primary, Label = "İlköğretim" },
                new EnumOptionDto { Value = (int)EducationLevel.HighSchool, Label = "Lise" },
                new EnumOptionDto { Value = (int)EducationLevel.AssociateDegree, Label = "Ön lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Bachelor, Label = "Lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Master, Label = "Yüksek lisans" },
                new EnumOptionDto { Value = (int)EducationLevel.Doctorate, Label = "Doktora" }
            ],
            CompletionStatuses =
            [
                new EnumOptionDto { Value = (int)EducationCompletionStatus.Ongoing, Label = "Devam ediyor" },
                new EnumOptionDto { Value = (int)EducationCompletionStatus.Graduated, Label = "Mezun" },
                new EnumOptionDto { Value = (int)EducationCompletionStatus.Suspended, Label = "Kayıt dondurmuş" },
                new EnumOptionDto { Value = (int)EducationCompletionStatus.DroppedOut, Label = "Yarım bırakmış" },
                new EnumOptionDto { Value = (int)EducationCompletionStatus.Unknown, Label = "Bilgi yok" }
            ]
        };

        return Task.FromResult(options);
    }

    private void EnsureEducationManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.EducationManage))
            throw new ForbiddenException("Eğitim yönetimi için Education.Manage gerekir.");
    }
}

public sealed class CreateEducationHandler : IRequestHandler<CreateEducationCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateEducationHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateEducationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EducationManage))
            throw new ForbiddenException("Eğitim eklemek için Education.Manage gerekir.");

        if (!await _db.Employees.AnyAsync(x => x.Id == request.EmployeeId, cancellationToken))
            throw new NotFoundException("Personel bulunamadı.");

        var actor = _currentUser.UserName ?? "system";
        var entity = new EducationRecord
        {
            EmployeeId = request.EmployeeId,
            CreatedBy = actor
        };
        EducationMapping.Apply(entity, request);

        _db.EducationRecords.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed class UpdateEducationHandler : IRequestHandler<UpdateEducationCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateEducationHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateEducationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EducationManage))
            throw new ForbiddenException("Eğitim güncellemek için Education.Manage gerekir.");

        var entity = await EducationMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.EducationId, cancellationToken);

        EducationMapping.Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteEducationHandler : IRequestHandler<DeleteEducationCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteEducationHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteEducationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EducationManage))
            throw new ForbiddenException("Eğitim silmek için Education.Manage gerekir.");

        var entity = await EducationMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.EducationId, cancellationToken);

        // Soft-delete: satır kalır → audit / geri alma mümkün
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Handler’lar arası ortak mapping / sahiplik kontrolü.</summary>
internal static class EducationMapping
{
    public static async Task<EducationRecord> FindOwnedAsync(
        AppDbContext db,
        Guid employeeId,
        Guid educationId,
        CancellationToken ct)
    {
        var entity = await db.EducationRecords
            .FirstOrDefaultAsync(x => x.Id == educationId && x.EmployeeId == employeeId, ct);

        // IDOR: başka personelin GUID’i → kasıtlı olarak "bulunamadı"
        return entity ?? throw new NotFoundException("Eğitim kaydı bulunamadı.");
    }

    public static void Apply(EducationRecord entity, UpsertEducationRequest request)
    {
        entity.Level = request.Level;
        entity.University = Normalize(request.University);
        entity.Faculty = Normalize(request.Faculty);
        entity.School = Normalize(request.School);
        entity.Department = Normalize(request.Department);
        entity.Program = Normalize(request.Program);
        entity.GraduationYear = request.GraduationYear;
        entity.CompletionStatus = request.CompletionStatus;
        entity.DiplomaNumber = Normalize(request.DiplomaNumber);
        entity.Description = Normalize(request.Description);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
