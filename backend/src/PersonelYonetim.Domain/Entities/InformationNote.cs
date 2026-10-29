using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Directorate information memorandum with optional project/event link and private source document.</summary>
public class InformationNote : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? FileData { get; set; }
}
