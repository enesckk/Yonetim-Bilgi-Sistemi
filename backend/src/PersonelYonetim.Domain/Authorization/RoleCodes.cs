namespace PersonelYonetim.Domain.Authorization;

/// <summary>
/// Rol kodları — veritabanında Roles.Code ile eşleşir.
/// Magic string yazmamak için sabitler kullanırız.
/// </summary>
public static class RoleCodes
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string DeputyMayor = "DEPUTY_MAYOR";
    public const string Director = "DIRECTOR";
    public const string DeputyDirector = "DEPUTY_DIRECTOR";
    public const string UnitManager = "UNIT_MANAGER";
    public const string DataEntry = "DATA_ENTRY";
    public const string Viewer = "VIEWER";

    public static IReadOnlyList<(string Code, string Name, string Description)> All { get; } =
    [
        (SystemAdmin, "Sistem Yöneticisi",
            "Tüm sistemi yönetir; kullanıcı oluşturur, rol/yetki tanımlar, birim-tesis ve ayarları yönetir, işlem geçmişini görür."),
        (DeputyMayor, "Başkan Yardımcısı",
            "Müdürlüğün genel verilerini görür; rapor ve personel dağılım analizlerine erişir; yetki verilen yönetici notlarını görür."),
        (Director, "Müdür",
            "Müdürlükteki tüm personelleri görür ve günceller; birim/tesis yönetimi, görevlendirme, yönetici notu, özel durum ve rapor oluşturma."),
        (DeputyDirector, "Müdür Yardımcısı",
            "Yetki verilen birimleri yönetir; personel görüntüleme/güncelleme, görev ve not ekleme, birim raporları."),
        (UnitManager, "Birim Sorumlusu",
            "Yalnızca kendi birimindeki personelleri görür; temel iletişim ve görev bilgisi; görev notu ekleyebilir; hassas bilgileri göremez."),
        (DataEntry, "Veri Giriş Personeli",
            "Personel ekler/günceller; yetkinlik ve eğitim girer; raporları sınırlı görür; özel durum ve yönetici notlarını göremez."),
        (Viewer, "Görüntüleme Kullanıcısı",
            "Yalnızca yetki verilen bilgileri görüntüler; veri değiştiremez; not ve hassas bilgileri göremez.")
    ];
}
