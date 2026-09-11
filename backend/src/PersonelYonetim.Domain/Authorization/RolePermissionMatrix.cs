namespace PersonelYonetim.Domain.Authorization;

/// <summary>
/// Rol → yetki matrisi.
/// Gerçek erişim kontrolü bu matrise göre yapılır (seed + runtime).
/// </summary>
public static class RolePermissionMatrix
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        // Sistem yöneticisi: her şey
        [RoleCodes.SystemAdmin] = PermissionCatalog.All.Select(p => p.Code).ToArray(),

        // Başkan yardımcısı: geniş görünüm, yazma sınırlı
        [RoleCodes.DeputyMayor] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesViewAllUnits,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.MovementsView,
            PermissionCodes.NotesViewDirectorate,
            PermissionCodes.NotesViewManager,
            PermissionCodes.OrganizationView,
            PermissionCodes.ReportsView,
            PermissionCodes.ReportsExportExcel,
            PermissionCodes.ReportsExportPdf,
            PermissionCodes.DataQualityView,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.FilesView,
            PermissionCodes.EventsView
        ],

        // Müdür: tam müdürlük yönetimi (TCKN / özel durum dahil yetkili)
        [RoleCodes.Director] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesCreate,
            PermissionCodes.EmployeesUpdate,
            PermissionCodes.EmployeesSetStatus,
            PermissionCodes.EmployeesArchive,
            PermissionCodes.EmployeesViewAllUnits,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.EmployeesViewAddress,
            PermissionCodes.EmployeesViewNationalId,
            PermissionCodes.EmployeesViewSpecialConditions,
            PermissionCodes.EmployeesManageSpecialConditions,
            PermissionCodes.AssignmentsManage,
            PermissionCodes.MovementsView,
            PermissionCodes.MovementsCreate,
            PermissionCodes.EducationManage,
            PermissionCodes.SkillsManage,
            PermissionCodes.CertificatesManage,
            PermissionCodes.CertificatesViewFiles,
            PermissionCodes.CatalogsManage,
            PermissionCodes.NotesViewOwn,
            PermissionCodes.NotesViewUnit,
            PermissionCodes.NotesViewDirectorate,
            PermissionCodes.NotesViewManager,
            PermissionCodes.NotesCreate,
            PermissionCodes.NotesCreateManager,
            PermissionCodes.OrganizationView,
            PermissionCodes.OrganizationManage,
            PermissionCodes.StockView,
            PermissionCodes.TasksView,
            PermissionCodes.TasksAssign,
            PermissionCodes.TasksReview,
            PermissionCodes.TasksSubmit,
            PermissionCodes.ReportsView,
            PermissionCodes.ReportsExportExcel,
            PermissionCodes.ReportsExportPdf,
            PermissionCodes.DataQualityView,
            PermissionCodes.ImportExcel,
            PermissionCodes.FilesView,
            PermissionCodes.FilesUpload,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.AuditLogsView,
            PermissionCodes.EventsView,
            PermissionCodes.EventsManage
        ],

        // Müdür yardımcısı: yazma var; TCKN ve özel durum yok
        [RoleCodes.DeputyDirector] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesCreate,
            PermissionCodes.EmployeesUpdate,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.EmployeesViewAddress,
            PermissionCodes.AssignmentsManage,
            PermissionCodes.MovementsView,
            PermissionCodes.MovementsCreate,
            PermissionCodes.EducationManage,
            PermissionCodes.SkillsManage,
            PermissionCodes.CertificatesManage,
            PermissionCodes.CertificatesViewFiles,
            PermissionCodes.CatalogsManage,
            PermissionCodes.NotesViewOwn,
            PermissionCodes.NotesViewUnit,
            PermissionCodes.NotesViewDirectorate,
            PermissionCodes.NotesCreate,
            PermissionCodes.OrganizationView,
            PermissionCodes.StockView,
            PermissionCodes.TasksView,
            PermissionCodes.TasksAssign,
            PermissionCodes.TasksReview,
            PermissionCodes.TasksSubmit,
            PermissionCodes.ReportsView,
            PermissionCodes.ReportsExportExcel,
            PermissionCodes.ReportsExportPdf,
            PermissionCodes.DataQualityView,
            PermissionCodes.FilesView,
            PermissionCodes.FilesUpload,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.EventsView,
            PermissionCodes.EventsManage
        ],

        // İdari amir: yalnızca kendi tesis ağacı (ViewAllUnits yok); kendi kadrosunu günceller
        [RoleCodes.AdministrativeOfficer] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesUpdate,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.EmployeesViewAddress,
            PermissionCodes.MovementsView,
            PermissionCodes.NotesViewOwn,
            PermissionCodes.NotesViewUnit,
            PermissionCodes.NotesCreate,
            PermissionCodes.OrganizationView,
            PermissionCodes.StockView,
            PermissionCodes.StockManage,
            PermissionCodes.TasksView,
            PermissionCodes.TasksSubmit,
            PermissionCodes.FilesView,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.EventsView,
            PermissionCodes.EventsManage
        ],

        // Birim amiri: sadece kendi birimi ve altı (ViewAllUnits YOK)
        [RoleCodes.UnitManager] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.MovementsView,
            PermissionCodes.NotesViewOwn,
            PermissionCodes.NotesViewUnit,
            PermissionCodes.NotesCreate,
            PermissionCodes.OrganizationView,
            PermissionCodes.StockView,
            PermissionCodes.StockManage,
            PermissionCodes.TasksView,
            PermissionCodes.TasksSubmit,
            PermissionCodes.FilesView,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.EventsView
        ],

        // Veri giriş: yazma var, hassas/not yok
        [RoleCodes.DataEntry] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.EmployeesCreate,
            PermissionCodes.EmployeesUpdate,
            PermissionCodes.EmployeesViewPhone,
            PermissionCodes.EducationManage,
            PermissionCodes.SkillsManage,
            PermissionCodes.CertificatesManage,
            PermissionCodes.CatalogsManage,
            PermissionCodes.OrganizationView,
            PermissionCodes.ReportsView,
            PermissionCodes.DataQualityView,
            PermissionCodes.ImportExcel,
            PermissionCodes.FilesView,
            PermissionCodes.FilesUpload,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.EventsView,
            PermissionCodes.EventsManage
        ],

        // Salt görüntüleme
        [RoleCodes.Viewer] =
        [
            PermissionCodes.DashboardView,
            PermissionCodes.EmployeesView,
            PermissionCodes.OrganizationView,
            PermissionCodes.ReportsView,
            PermissionCodes.NotificationsView,
            PermissionCodes.MessagesUse,
            PermissionCodes.EventsView
        ]
    };

    public static IReadOnlyDictionary<string, string[]> GetMap() => Map;

    public static IReadOnlyList<string> GetPermissionsForRole(string roleCode)
    {
        return Map.TryGetValue(roleCode, out var permissions)
            ? permissions
            : Array.Empty<string>();
    }

    public static bool RoleHasPermission(string roleCode, string permissionCode)
    {
        return GetPermissionsForRole(roleCode).Contains(permissionCode);
    }
}
