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
/// Not MediatR handlers — eski NoteCommandService'in parçalanmış hali.
/// Görünürlük ve satır bazlı erişim kuralları NoteAccessRules içinde korunur.
/// </summary>
public sealed class GetNoteFormOptionsHandler
    : IRequestHandler<GetNoteFormOptionsQuery, NoteFormOptionsDto>
{
    private readonly ICurrentUserService _currentUser;

    public GetNoteFormOptionsHandler(ICurrentUserService currentUser) =>
        _currentUser = currentUser;

    public Task<NoteFormOptionsDto> Handle(
        GetNoteFormOptionsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureNotesCreate();

        var allCategories = new (NoteCategory Value, string Label)[]
        {
            (NoteCategory.General, "Genel"),
            (NoteCategory.Manager, "Yönetici"),
            (NoteCategory.Interview, "Görüşme"),
            (NoteCategory.Assignment, "Görevlendirme"),
            (NoteCategory.Performance, "Performans"),
            (NoteCategory.Training, "Eğitim"),
            (NoteCategory.Communication, "İletişim"),
            (NoteCategory.Duty, "Görev"),
            (NoteCategory.SpecialSituation, "Özel durum"),
            (NoteCategory.Reminder, "Hatırlatma"),
            (NoteCategory.InstitutionalDevelopment, "Kurumsal gelişim")
        };

        var categories = allCategories
            .Where(c => c.Value != NoteCategory.Manager
                        || _currentUser.HasPermission(PermissionCodes.NotesCreateManager))
            .Select(c => new EnumOptionDto { Value = (int)c.Value, Label = c.Label })
            .ToList();

        var allVisibilities = new (NoteVisibility Value, string Label)[]
        {
            (NoteVisibility.AuthorOnly, "Yalnızca yazar"),
            (NoteVisibility.UnitManagers, "Birim yöneticileri"),
            (NoteVisibility.DirectorateManagers, "Müdürlük yöneticileri"),
            (NoteVisibility.PrivilegedUsers, "Yetkili kullanıcılar"),
            (NoteVisibility.SystemAdministrators, "Sistem yöneticileri")
        };

        var visibilities = allVisibilities
            .Where(v => NoteAccessRules.CanAssignVisibility(v.Value, _currentUser))
            .Select(v => new EnumOptionDto { Value = (int)v.Value, Label = v.Label })
            .ToList();

        return Task.FromResult(new NoteFormOptionsDto
        {
            Categories = categories,
            Visibilities = visibilities
        });
    }

    private void EnsureNotesCreate()
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotesCreate))
            throw new ForbiddenException("Not oluşturma için Notes.Create gerekir.");
    }
}

public sealed class CreateNoteHandler : IRequestHandler<CreateNoteCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateNoteHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        NoteMapping.EnsureCanCreate(request, _currentUser);

        if (!await _db.Employees.AnyAsync(x => x.Id == request.EmployeeId, cancellationToken))
            throw new NotFoundException("Personel bulunamadı.");

        var entity = new EmployeeNote
        {
            EmployeeId = request.EmployeeId,
            NoteDateUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        NoteMapping.Apply(entity, request);

        _db.EmployeeNotes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed class UpdateNoteHandler : IRequestHandler<UpdateNoteCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateNoteHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var entity = await NoteMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.NoteId, cancellationToken);

        if (!NoteAccessRules.CanModify(entity, _currentUser))
            throw new ForbiddenException("Bu notu düzenleme yetkiniz yok.");

        NoteMapping.EnsureCanCreate(request, _currentUser);
        NoteMapping.Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteNoteHandler : IRequestHandler<DeleteNoteCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteNoteHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var entity = await NoteMapping.FindOwnedAsync(
            _db, request.EmployeeId, request.NoteId, cancellationToken);

        if (!NoteAccessRules.CanModify(entity, _currentUser))
            throw new ForbiddenException("Bu notu silme yetkiniz yok.");

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.DeletedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class NoteMapping
{
    public static async Task<EmployeeNote> FindOwnedAsync(
        AppDbContext db,
        Guid employeeId,
        Guid noteId,
        CancellationToken ct)
    {
        var entity = await db.EmployeeNotes
            .FirstOrDefaultAsync(x => x.Id == noteId && x.EmployeeId == employeeId, ct);

        return entity ?? throw new NotFoundException("Not bulunamadı.");
    }

    public static void EnsureCanCreate(UpsertNoteRequest request, ICurrentUserService currentUser)
    {
        if (!NoteAccessRules.CanCreate(request.Category, request.Visibility, currentUser))
            throw new ForbiddenException(
                "Bu kategori veya görünürlük seviyesi için not oluşturma yetkiniz yok.");
    }

    public static void Apply(EmployeeNote entity, UpsertNoteRequest request)
    {
        entity.Title = request.Title.Trim();
        entity.Category = request.Category;
        entity.Content = request.Content.Trim();
        entity.Visibility = request.Visibility;
        entity.ReminderDate = request.ReminderDate;
    }
}
