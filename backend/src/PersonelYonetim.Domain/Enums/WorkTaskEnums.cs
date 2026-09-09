namespace PersonelYonetim.Domain.Enums;

public enum WorkTaskKind
{
    /// <summary>Müdür / yönetici personel veya amire iş verir.</summary>
    Assignment = 1,
    /// <summary>İdari amir müdür onayına iş/görsel sunar.</summary>
    ApprovalRequest = 2
}

public enum WorkTaskStatus
{
    Open = 1,
    InProgress = 2,
    WaitingApproval = 3,
    RevisionRequested = 4,
    Approved = 5,
    Rejected = 6,
    Done = 7,
    Cancelled = 8
}

public static class WorkTaskLabels
{
    public static string Kind(WorkTaskKind kind) => kind switch
    {
        WorkTaskKind.Assignment => "İş ataması",
        WorkTaskKind.ApprovalRequest => "Onay talebi",
        _ => kind.ToString()
    };

    public static string Status(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Open => "Açık",
        WorkTaskStatus.InProgress => "Devam ediyor",
        WorkTaskStatus.WaitingApproval => "Onay bekliyor",
        WorkTaskStatus.RevisionRequested => "Revizyon",
        WorkTaskStatus.Approved => "Onaylandı",
        WorkTaskStatus.Rejected => "Reddedildi",
        WorkTaskStatus.Done => "Tamamlandı",
        WorkTaskStatus.Cancelled => "İptal",
        _ => status.ToString()
    };
}
