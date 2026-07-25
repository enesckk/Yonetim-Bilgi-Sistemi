using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Settings;

/// <summary>Bilinen ayar anahtarları — kod ve seed aynı string’i kullanır.</summary>
public static class AppSettingKeys
{
    public const string OrganizationDisplayName = "Organization.DisplayName";
    public const string ReportsExcelMaxRows = "Reports.ExcelMaxRows";
    public const string ReportsPdfMaxRows = "Reports.PdfMaxRows";
    public const string DataQualityActiveOnlyDefault = "DataQuality.ActiveOnlyDefault";
    public const string NotificationsEmailEnabled = "Notifications.EmailEnabled";
}

public sealed record AppSettingDefinition(
    string Key,
    string DefaultValue,
    AppSettingValueType ValueType,
    string GroupName,
    string DisplayName,
    string Description,
    bool IsReadOnly = false);

/// <summary>Seed kataloğu — eksik anahtarlar açılışta eklenir.</summary>
public static class AppSettingCatalog
{
    public static IReadOnlyList<AppSettingDefinition> All { get; } =
    [
        new(
            AppSettingKeys.OrganizationDisplayName,
            "Şehitkamil Kültür Müdürlüğü",
            AppSettingValueType.String,
            "Kurum",
            "Kurum görünür adı",
            "PDF başlıkları ve kurumsal çıktılarda görünen ad."),
        new(
            AppSettingKeys.ReportsExcelMaxRows,
            "5000",
            AppSettingValueType.Integer,
            "Raporlar",
            "Excel satır üst sınırı",
            "Excel dışa aktarımında tek seferde indirilebilecek en fazla satır sayısı. Çok yüksek değer belleği zorlayabilir."),
        new(
            AppSettingKeys.ReportsPdfMaxRows,
            "1000",
            AppSettingValueType.Integer,
            "Raporlar",
            "PDF satır üst sınırı",
            "PDF dışa aktarımında tek seferde indirilebilecek en fazla satır sayısı. PDF daha ağır olduğu için Excel’den düşük tutulur."),
        new(
            AppSettingKeys.DataQualityActiveOnlyDefault,
            "true",
            AppSettingValueType.Boolean,
            "Veri Kalitesi",
            "Varsayılan: yalnızca aktif personel",
            "Veri kalitesi ekranı açıldığında yalnızca aktif personeli listelesin."),
        new(
            AppSettingKeys.NotificationsEmailEnabled,
            "false",
            AppSettingValueType.Boolean,
            "Bildirimler",
            "E-posta bildirimi",
            "Kritik uyarılar e-posta ile de gönderilsin. Sunucuda e-posta (SMTP) ayarı gerekir; uygulama içi bildirimler her zaman aktiftir.")
    ];
}
