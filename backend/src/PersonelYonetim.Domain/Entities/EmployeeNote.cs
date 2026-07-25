using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

public class EmployeeNote : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public NoteCategory Category { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime NoteDateUtc { get; set; } = DateTime.UtcNow;
    public NoteVisibility Visibility { get; set; } = NoteVisibility.UnitManagers;
    public DateOnly? ReminderDate { get; set; }
    public string? AttachmentPath { get; set; }
}
