namespace PersonelYonetim.Domain.Authorization;

/// <summary>
/// Yetki kodları — API ve UI aynı string'i kullanır.
/// Biçim: Alan.İşlem  (ör. Employees.ViewNationalId)
/// </summary>
public static class PermissionCodes
{
    // Dashboard
    public const string DashboardView = "Dashboard.View";

    // Personel — işlem
    public const string EmployeesView = "Employees.View";
    public const string EmployeesCreate = "Employees.Create";
    public const string EmployeesUpdate = "Employees.Update";
    public const string EmployeesSetStatus = "Employees.SetStatus";
    public const string EmployeesArchive = "Employees.Archive";
    public const string EmployeesViewAllUnits = "Employees.ViewAllUnits";

    // Personel — alan bazlı (hassas)
    public const string EmployeesViewPhone = "Employees.ViewPhone";
    public const string EmployeesViewAddress = "Employees.ViewAddress";
    public const string EmployeesViewNationalId = "Employees.ViewNationalId";
    public const string EmployeesViewSpecialConditions = "Employees.ViewSpecialConditions";
    public const string EmployeesManageSpecialConditions = "Employees.ManageSpecialConditions";

    // Görev / hareket
    public const string AssignmentsManage = "Assignments.Manage";
    public const string MovementsView = "Movements.View";
    public const string MovementsCreate = "Movements.Create";

    // Eğitim / yetkinlik / sertifika
    public const string EducationManage = "Education.Manage";
    public const string SkillsManage = "Skills.Manage";
    public const string CertificatesManage = "Certificates.Manage";
    public const string CertificatesViewFiles = "Certificates.ViewFiles";

    // Katalog tanımları (fiili görev, unvan, istihdam, tesis türü)
    public const string CatalogsManage = "Catalogs.Manage";

    // Notlar
    public const string NotesViewOwn = "Notes.ViewOwn";
    public const string NotesViewUnit = "Notes.ViewUnit";
    public const string NotesViewDirectorate = "Notes.ViewDirectorate";
    public const string NotesViewManager = "Notes.ViewManager";
    public const string NotesCreate = "Notes.Create";
    public const string NotesCreateManager = "Notes.CreateManager";

    // Organizasyon
    public const string OrganizationView = "Organization.View";
    public const string OrganizationManage = "Organization.Manage";

    // Raporlar
    public const string ReportsView = "Reports.View";
    public const string ReportsExportExcel = "Reports.ExportExcel";
    public const string ReportsExportPdf = "Reports.ExportPdf";

    // Veri kalitesi / import
    public const string DataQualityView = "DataQuality.View";
    public const string ImportExcel = "Import.Excel";

    // Dosyalar
    public const string FilesView = "Files.View";
    public const string FilesUpload = "Files.Upload";

    // Sistem
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string AuditLogsView = "AuditLogs.View";
    public const string NotificationsView = "Notifications.View";
    public const string SettingsManage = "Settings.Manage";
}
