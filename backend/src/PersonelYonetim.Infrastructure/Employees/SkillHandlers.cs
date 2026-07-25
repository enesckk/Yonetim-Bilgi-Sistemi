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

/// <summary>
/// Skills MediatR handlers — eski SkillCommandService’in parçalanmış hali.
/// Her handler tek iş: Create / Update / Delete / FormOptions.
/// </summary>
public sealed class GetSkillFormOptionsHandler
    : IRequestHandler<GetSkillFormOptionsQuery, SkillFormOptionsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSkillFormOptionsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SkillFormOptionsDto> Handle(
        GetSkillFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureSkillsManage();

        var skills = await _db.Skills
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new SkillLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                CategoryLabel = ToCategoryLabel(x.Category)
            })
            .ToListAsync(cancellationToken);

        return new SkillFormOptionsDto
        {
            Skills = skills,
            Levels =
            [
                new EnumOptionDto { Value = (int)SkillLevel.Beginner, Label = "Başlangıç" },
                new EnumOptionDto { Value = (int)SkillLevel.Intermediate, Label = "Orta" },
                new EnumOptionDto { Value = (int)SkillLevel.Advanced, Label = "İleri" },
                new EnumOptionDto { Value = (int)SkillLevel.Expert, Label = "Uzman" }
            ]
        };
    }

    private void EnsureSkillsManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik yönetimi için Skills.Manage gerekir.");
    }

    private static string ToCategoryLabel(SkillCategory category) => category switch
    {
        SkillCategory.Technical => "Teknik",
        SkillCategory.EducationWorkshop => "Eğitim / Atölye",
        SkillCategory.Administrative => "İdari",
        SkillCategory.Communication => "İletişim",
        SkillCategory.Language => "Dil",
        _ => category.ToString()
    };
}

public sealed class CreateEmployeeSkillHandler : IRequestHandler<CreateEmployeeSkillCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateEmployeeSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateEmployeeSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik eklemek için Skills.Manage gerekir.");

        await EnsureEmployeeExistsAsync(request.EmployeeId, cancellationToken);
        await EnsureSkillExistsAsync(request.SkillId, cancellationToken);
        await EnsureUniqueAsync(request.EmployeeId, request.SkillId, excludeId: null, cancellationToken);

        var actor = _currentUser.UserName ?? "system";
        var entity = new EmployeeSkill
        {
            EmployeeId = request.EmployeeId,
            CreatedBy = actor
        };
        Apply(entity, request);

        _db.EmployeeSkills.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private async Task EnsureEmployeeExistsAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await _db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException("Personel bulunamadı.");
    }

    private async Task EnsureSkillExistsAsync(Guid skillId, CancellationToken ct)
    {
        if (!await _db.Skills.AnyAsync(x => x.Id == skillId && x.IsActive, ct))
            throw new AppValidationException(nameof(UpsertEmployeeSkillRequest.SkillId), "Seçilen yetkinlik bulunamadı veya pasif.");
    }

    private async Task EnsureUniqueAsync(Guid employeeId, Guid skillId, Guid? excludeId, CancellationToken ct)
    {
        var exists = await _db.EmployeeSkills.AnyAsync(
            x => x.EmployeeId == employeeId
                 && x.SkillId == skillId
                 && (!excludeId.HasValue || x.Id != excludeId),
            ct);

        if (exists)
            throw new ConflictException("Bu yetkinlik personelde zaten tanımlı. Seviyeyi düzenleyebilirsiniz.");
    }

    private static void Apply(EmployeeSkill entity, UpsertEmployeeSkillRequest request)
    {
        entity.SkillId = request.SkillId;
        entity.Level = request.Level;
        entity.ExperienceDuration = Normalize(request.ExperienceDuration);
        entity.HasCertificate = request.HasCertificate;
        entity.CertificateDate = request.HasCertificate ? request.CertificateDate : null;
        entity.CertificateIssuer = request.HasCertificate ? Normalize(request.CertificateIssuer) : null;
        entity.Description = Normalize(request.Description);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateEmployeeSkillHandler : IRequestHandler<UpdateEmployeeSkillCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateEmployeeSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateEmployeeSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik güncellemek için Skills.Manage gerekir.");

        await EnsureSkillExistsAsync(request.SkillId, cancellationToken);

        var entity = await FindOwnedAsync(request.EmployeeId, request.EmployeeSkillId, cancellationToken);
        await EnsureUniqueAsync(request.EmployeeId, request.SkillId, excludeId: request.EmployeeSkillId, cancellationToken);

        Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSkillExistsAsync(Guid skillId, CancellationToken ct)
    {
        if (!await _db.Skills.AnyAsync(x => x.Id == skillId && x.IsActive, ct))
            throw new AppValidationException(nameof(UpsertEmployeeSkillRequest.SkillId), "Seçilen yetkinlik bulunamadı veya pasif.");
    }

    private async Task EnsureUniqueAsync(Guid employeeId, Guid skillId, Guid? excludeId, CancellationToken ct)
    {
        var exists = await _db.EmployeeSkills.AnyAsync(
            x => x.EmployeeId == employeeId
                 && x.SkillId == skillId
                 && (!excludeId.HasValue || x.Id != excludeId),
            ct);

        if (exists)
            throw new ConflictException("Bu yetkinlik personelde zaten tanımlı. Seviyeyi düzenleyebilirsiniz.");
    }

    private async Task<EmployeeSkill> FindOwnedAsync(Guid employeeId, Guid employeeSkillId, CancellationToken ct)
    {
        var entity = await _db.EmployeeSkills
            .FirstOrDefaultAsync(x => x.Id == employeeSkillId && x.EmployeeId == employeeId, ct);

        return entity ?? throw new NotFoundException("Yetkinlik kaydı bulunamadı.");
    }

    private static void Apply(EmployeeSkill entity, UpsertEmployeeSkillRequest request)
    {
        entity.SkillId = request.SkillId;
        entity.Level = request.Level;
        entity.ExperienceDuration = Normalize(request.ExperienceDuration);
        entity.HasCertificate = request.HasCertificate;
        entity.CertificateDate = request.HasCertificate ? request.CertificateDate : null;
        entity.CertificateIssuer = request.HasCertificate ? Normalize(request.CertificateIssuer) : null;
        entity.Description = Normalize(request.Description);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DeleteEmployeeSkillHandler : IRequestHandler<DeleteEmployeeSkillCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteEmployeeSkillHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteEmployeeSkillCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.SkillsManage))
            throw new ForbiddenException("Yetkinlik silmek için Skills.Manage gerekir.");

        var entity = await _db.EmployeeSkills
            .FirstOrDefaultAsync(
                x => x.Id == request.EmployeeSkillId && x.EmployeeId == request.EmployeeId,
                cancellationToken)
            ?? throw new NotFoundException("Yetkinlik kaydı bulunamadı.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}
