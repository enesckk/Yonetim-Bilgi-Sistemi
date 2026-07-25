using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Employees;

public sealed class GetCertificateFormOptionsHandler
    : IRequestHandler<GetCertificateFormOptionsQuery, CertificateFormOptionsDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCertificateFormOptionsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CertificateFormOptionsDto> Handle(
        GetCertificateFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureCertificatesManage();

        var definitions = await _db.CertificateDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Category })
            .ToListAsync(cancellationToken);

        var skills = await _db.Skills
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);

        return new CertificateFormOptionsDto { Definitions = definitions, Skills = skills };
    }

    private void EnsureCertificatesManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika yönetimi için Certificates.Manage gerekir.");
    }
}

public sealed class CreateCertificateHandler : IRequestHandler<CreateCertificateCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateCertificateHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateCertificateCommand request, CancellationToken cancellationToken)
    {
        EnsureCertificatesManage();
        await CertificateMapping.EnsureEmployeeExistsAsync(_db, request.EmployeeId, cancellationToken);
        await CertificateMapping.EnsureLookupsAsync(_db, request, cancellationToken);

        var entity = new EmployeeCertificate
        {
            EmployeeId = request.EmployeeId,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        await CertificateMapping.ApplyAsync(_db, entity, request, cancellationToken);

        _db.EmployeeCertificates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private void EnsureCertificatesManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika eklemek için Certificates.Manage gerekir.");
    }
}

public sealed class UpdateCertificateHandler : IRequestHandler<UpdateCertificateCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCertificateHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateCertificateCommand request, CancellationToken cancellationToken)
    {
        EnsureCertificatesManage();
        await CertificateMapping.EnsureLookupsAsync(_db, request, cancellationToken);

        var entity = await CertificateMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.CertificateId, cancellationToken);
        await CertificateMapping.ApplyAsync(_db, entity, request, cancellationToken);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCertificatesManage()
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika güncellemek için Certificates.Manage gerekir.");
    }
}

public sealed class DeleteCertificateHandler : IRequestHandler<DeleteCertificateCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteCertificateHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteCertificateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.CertificatesManage))
            throw new ForbiddenException("Sertifika silmek için Certificates.Manage gerekir.");

        var entity = await CertificateMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.CertificateId, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class CertificateMapping
{
    public static async Task ApplyAsync(
        AppDbContext db,
        EmployeeCertificate entity,
        UpsertCertificateRequest request,
        CancellationToken ct)
    {
        entity.CertificateDefinitionId = request.CertificateDefinitionId;
        entity.RelatedSkillId = request.RelatedSkillId;
        entity.Issuer = Normalize(request.Issuer);
        entity.DocumentNumber = Normalize(request.DocumentNumber);
        entity.Description = Normalize(request.Description);
        entity.IssuedOn = request.IssuedOn;
        entity.ExpiresOn = request.ExpiresOn;

        var name = Normalize(request.Name);
        var category = Normalize(request.Category);
        if (request.CertificateDefinitionId.HasValue)
        {
            var definition = await db.CertificateDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CertificateDefinitionId, ct);
            if (definition is not null)
            {
                name ??= definition.Name;
                category ??= Normalize(definition.Category);
            }
        }

        entity.Name = name ?? throw new AppValidationException(
            nameof(request.Name), "Sertifika adı zorunludur.");
        entity.Category = category;
    }

    public static async Task EnsureEmployeeExistsAsync(AppDbContext db, Guid employeeId, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException("Personel bulunamadı.");
    }

    public static async Task EnsureLookupsAsync(
        AppDbContext db,
        UpsertCertificateRequest request,
        CancellationToken ct)
    {
        if (request.CertificateDefinitionId.HasValue
            && !await db.CertificateDefinitions.AnyAsync(
                x => x.Id == request.CertificateDefinitionId && x.IsActive, ct))
        {
            throw new AppValidationException(
                nameof(request.CertificateDefinitionId), "Sertifika tanımı bulunamadı veya pasif.");
        }

        if (request.RelatedSkillId.HasValue
            && !await db.Skills.AnyAsync(
                x => x.Id == request.RelatedSkillId && x.IsActive, ct))
        {
            throw new AppValidationException(
                nameof(request.RelatedSkillId), "İlişkili yetkinlik bulunamadı.");
        }
    }

    public static async Task<EmployeeCertificate> FindOwnedAsync(
        AppDbContext db,
        Guid employeeId,
        Guid certificateId,
        CancellationToken ct)
    {
        var entity = await db.EmployeeCertificates
            .FirstOrDefaultAsync(x => x.Id == certificateId && x.EmployeeId == employeeId, ct);
        return entity ?? throw new NotFoundException("Sertifika kaydı bulunamadı.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
