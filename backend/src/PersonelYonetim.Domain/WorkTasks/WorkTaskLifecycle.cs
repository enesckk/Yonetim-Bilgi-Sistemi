using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.WorkTasks;

public static class WorkTaskLifecycle
{
    public static bool IsClosed(WorkTaskStatus status) =>
        status is WorkTaskStatus.Approved
            or WorkTaskStatus.Rejected
            or WorkTaskStatus.Done
            or WorkTaskStatus.Cancelled;

    public static bool CanCancel(WorkTaskStatus status) => !IsClosed(status);

    public static bool CanDelete(WorkTaskStatus status) =>
        status is WorkTaskStatus.Open or WorkTaskStatus.Cancelled;
}
