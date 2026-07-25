using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Not erişim kuralları — endpoint yetkisi yetmez; her satır Visibility + yazar ile filtrelenir.
/// Bu, "row-level security" / ABAC benzeri bir uygulamadır.
/// </summary>
public static class NoteAccessRules
{
    /// <summary>Okuma: yazar her zaman görür; diğerleri Visibility ↔ View* yetkisine bağlı.</summary>
    public static bool CanView(EmployeeNote note, ICurrentUserService user)
    {
        if (!user.IsAuthenticated)
            return false;

        if (IsAuthor(note, user))
            return true;

        return note.Visibility switch
        {
            NoteVisibility.AuthorOnly => false,
            NoteVisibility.UnitManagers => user.HasPermission(PermissionCodes.NotesViewUnit),
            NoteVisibility.DirectorateManagers => user.HasPermission(PermissionCodes.NotesViewDirectorate),
            NoteVisibility.PrivilegedUsers => user.HasPermission(PermissionCodes.NotesViewManager),
            NoteVisibility.SystemAdministrators =>
                user.HasPermission(PermissionCodes.RolesManage)
                || user.HasPermission(PermissionCodes.UsersManage),
            _ => false
        };
    }

    /// <summary>Yazma sonrası düzenleme/silme: yazar veya yönetici not yetkisi.</summary>
    public static bool CanModify(EmployeeNote note, ICurrentUserService user)
    {
        if (!user.IsAuthenticated)
            return false;

        if (IsAuthor(note, user))
            return true;

        // Yönetici notu yazabilenler başkasının notunu da düzeltebilir (kurumsal denetim)
        return user.HasPermission(PermissionCodes.NotesCreateManager);
    }

    /// <summary>Oluşturma: kategori + görünürlük için ek yetki gerekebilir.</summary>
    public static bool CanCreate(NoteCategory category, NoteVisibility visibility, ICurrentUserService user)
    {
        if (!user.HasPermission(PermissionCodes.NotesCreate)
            && !user.HasPermission(PermissionCodes.NotesCreateManager))
            return false;

        if (category == NoteCategory.Manager
            && !user.HasPermission(PermissionCodes.NotesCreateManager))
            return false;

        return CanAssignVisibility(visibility, user);
    }

    /// <summary>
    /// Görünürlük atama: kullanıcının o seviyeyi "görebilecek" yetkisi olmalı
    /// (yüksek seviye notu yazıp kendisi görememe tuzağını engeller).
    /// </summary>
    public static bool CanAssignVisibility(NoteVisibility visibility, ICurrentUserService user)
    {
        return visibility switch
        {
            NoteVisibility.AuthorOnly => true,
            NoteVisibility.UnitManagers =>
                user.HasPermission(PermissionCodes.NotesViewUnit)
                || user.HasPermission(PermissionCodes.NotesCreateManager),
            NoteVisibility.DirectorateManagers =>
                user.HasPermission(PermissionCodes.NotesViewDirectorate)
                || user.HasPermission(PermissionCodes.NotesCreateManager),
            NoteVisibility.PrivilegedUsers =>
                user.HasPermission(PermissionCodes.NotesViewManager)
                || user.HasPermission(PermissionCodes.NotesCreateManager),
            NoteVisibility.SystemAdministrators =>
                user.HasPermission(PermissionCodes.RolesManage)
                || user.HasPermission(PermissionCodes.UsersManage),
            _ => false
        };
    }

    public static bool IsAuthor(EmployeeNote note, ICurrentUserService user) =>
        !string.IsNullOrWhiteSpace(user.UserName)
        && string.Equals(note.CreatedBy, user.UserName, StringComparison.OrdinalIgnoreCase);
}
