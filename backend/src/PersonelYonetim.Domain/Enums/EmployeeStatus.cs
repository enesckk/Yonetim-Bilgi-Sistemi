namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Personelin kurumsal durumu. Kayıt silinmez; durum güncellenir.
/// </summary>
public enum EmployeeStatus : byte
{
    Active = 1,
    Passive = 2,
    OnLeave = 3,
    LongTermLeave = 4,
    TemporaryAssignment = 5,
    LeftJob = 6,
    Retired = 7,
    TransferredToOtherDirectorate = 8,
    Suspended = 9,
    AwaitingReturn = 10,
    /// <summary>Görevden ayrıldı — kurumsal geçmiş korunur.</summary>
    DutyEnded = 11
}

public static class EmployeeStatusLabels
{
    public static string For(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Active => "Aktif",
        EmployeeStatus.Passive => "Pasif",
        EmployeeStatus.OnLeave => "İzinli",
        EmployeeStatus.LongTermLeave => "Uzun süreli izinli",
        EmployeeStatus.TemporaryAssignment => "Geçici görevli",
        EmployeeStatus.LeftJob => "İşten ayrıldı",
        EmployeeStatus.Retired => "Emekli oldu",
        EmployeeStatus.TransferredToOtherDirectorate => "Başka müdürlüğe geçti",
        EmployeeStatus.Suspended => "Askıda",
        EmployeeStatus.AwaitingReturn => "Göreve dönmesi bekleniyor",
        EmployeeStatus.DutyEnded => "Görevden ayrıldı",
        _ => status.ToString()
    };

    public static bool IsDeparture(EmployeeStatus status) => status is
        EmployeeStatus.LeftJob
        or EmployeeStatus.Retired
        or EmployeeStatus.TransferredToOtherDirectorate
        or EmployeeStatus.DutyEnded
        or EmployeeStatus.Passive;
}
