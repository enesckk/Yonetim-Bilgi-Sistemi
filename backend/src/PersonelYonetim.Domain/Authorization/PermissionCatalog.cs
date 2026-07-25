namespace PersonelYonetim.Domain.Authorization;

public sealed record PermissionDefinition(
    string Code,
    string Name,
    string GroupName,
    string Description);

/// <summary>
/// Sistemdeki tüm yetkilerin kataloğu.
/// Seed ve dokümantasyon buradan üretilir.
/// </summary>
public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(PermissionCodes.DashboardView, "Kontrol paneli görüntüleme", "Dashboard", "Özet kartlar ve grafikler"),

        new(PermissionCodes.EmployeesView, "Personel listesi / profil görüntüleme", "Personel", "Temel personel bilgilerini görür"),
        new(PermissionCodes.EmployeesCreate, "Personel ekleme", "Personel", "Yeni personel kaydı oluşturur"),
        new(PermissionCodes.EmployeesUpdate, "Personel düzenleme", "Personel", "Personel bilgilerini günceller"),
        new(PermissionCodes.EmployeesSetStatus, "Personel durum değiştirme", "Personel", "Aktif/pasif/ayrıldı vb."),
        new(PermissionCodes.EmployeesArchive, "Personel arşivleme", "Personel", "Yanlış oluşturulmuş kaydı yalnızca sistem yöneticisi arşivler"),
        new(PermissionCodes.EmployeesViewAllUnits, "Tüm birimleri görme", "Personel", "Birim sınırlaması olmadan tüm personel"),

        new(PermissionCodes.EmployeesViewPhone, "Telefon görüntüleme", "Hassas Alan", "Telefon numaralarını görür"),
        new(PermissionCodes.EmployeesViewAddress, "Adres görüntüleme", "Hassas Alan", "Adres bilgisini görür"),
        new(PermissionCodes.EmployeesViewNationalId, "T.C. kimlik görüntüleme", "Hassas Alan", "Maskesiz TCKN erişimi"),
        new(PermissionCodes.EmployeesViewSpecialConditions, "Özel durum görüntüleme", "Hassas Alan", "Özel durum detayını görür"),
        new(PermissionCodes.EmployeesManageSpecialConditions, "Özel durum yönetme", "Hassas Alan", "Özel durum ekler/günceller"),

        new(PermissionCodes.AssignmentsManage, "Görev ataması yönetme", "Görev", "Ana/ek görev tanımlar"),
        new(PermissionCodes.MovementsView, "Hareket geçmişi görüntüleme", "Görev", "Birim/görev geçmişini görür"),
        new(PermissionCodes.MovementsCreate, "Hareket oluşturma", "Görev", "Görev/birim değişikliği kaydı"),

        new(PermissionCodes.EducationManage, "Eğitim bilgisi yönetme", "Profil", "Eğitim kayıtları"),
        new(PermissionCodes.SkillsManage, "Yetkinlik yönetme", "Profil", "Yetkinlik ekleme/düzenleme"),
        new(PermissionCodes.CertificatesManage, "Sertifika yönetme", "Profil", "Sertifika ekleme/düzenleme"),
        new(PermissionCodes.CertificatesViewFiles, "Sertifika dosyası görüntüleme", "Profil", "Belge dosyalarına erişim"),
        new(PermissionCodes.CatalogsManage, "Katalog yönetimi", "Profil", "Fiili görev, unvan, istihdam ve tesis türü tanımları"),

        new(PermissionCodes.NotesViewOwn, "Kendi notlarını görme", "Notlar", "Yalnızca kendi eklediği notlar"),
        new(PermissionCodes.NotesViewUnit, "Birim notlarını görme", "Notlar", "Birim yöneticisi seviyesindeki notlar"),
        new(PermissionCodes.NotesViewDirectorate, "Müdürlük notlarını görme", "Notlar", "Müdür / müdür yardımcısı notları"),
        new(PermissionCodes.NotesViewManager, "Yönetici notlarını görme", "Notlar", "Yönetici kategorisindeki notlar"),
        new(PermissionCodes.NotesCreate, "Not ekleme", "Notlar", "Genel / görev notu ekler"),
        new(PermissionCodes.NotesCreateManager, "Yönetici notu ekleme", "Notlar", "Yönetici notu kategorisi"),

        new(PermissionCodes.OrganizationView, "Birim/tesis görüntüleme", "Organizasyon", "Organizasyon ağacını görür"),
        new(PermissionCodes.OrganizationManage, "Birim/tesis yönetme", "Organizasyon", "Birim ve tesis CRUD"),

        new(PermissionCodes.ReportsView, "Rapor görüntüleme", "Raporlar", "Hazır raporları görür"),
        new(PermissionCodes.ReportsExportExcel, "Excel dışa aktarma", "Raporlar", "Excel çıktısı alır"),
        new(PermissionCodes.ReportsExportPdf, "PDF dışa aktarma", "Raporlar", "PDF çıktısı alır"),

        new(PermissionCodes.DataQualityView, "Veri eksikleri görüntüleme", "Veri Kalitesi", "Eksik bilgi uyarıları"),
        new(PermissionCodes.ImportExcel, "Excel toplu aktarım", "Veri Kalitesi", "Toplu personel yükleme"),

        new(PermissionCodes.FilesView, "Dosya görüntüleme", "Dosyalar", "Yetkili dosyalara erişim"),
        new(PermissionCodes.FilesUpload, "Dosya yükleme", "Dosyalar", "Belge/fotoğraf yükleme"),

        new(PermissionCodes.UsersManage, "Kullanıcı yönetimi", "Sistem", "Kullanıcı oluşturma/düzenleme"),
        new(PermissionCodes.RolesManage, "Rol ve yetki yönetimi", "Sistem", "Rol–yetki ataması"),
        new(PermissionCodes.AuditLogsView, "İşlem geçmişi", "Sistem", "Audit log görüntüleme"),
        new(PermissionCodes.NotificationsView, "Bildirimler", "Sistem", "Sistem bildirimlerini görme"),
        new(PermissionCodes.SettingsManage, "Sistem ayarları", "Sistem", "Uygulama ayarları")
    ];
}
