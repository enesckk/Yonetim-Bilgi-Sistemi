using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Çalışma zamanı iş ayarı (DB).
/// appsettings.json / IOptions = dağıtım config (JWT, connection string);
/// AppSetting = admin’in UI’dan değiştirdiği iş kuralları.
/// </summary>
public class AppSetting : AuditableEntity
{
    /// <summary>Benzersiz anahtar — örn. Reports.ExcelMaxRows</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public AppSettingValueType ValueType { get; set; } = AppSettingValueType.String;

    public string GroupName { get; set; } = "Genel";
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>true ise UI’dan değiştirilemez (salt okunur bilgi).</summary>
    public bool IsReadOnly { get; set; }
}
