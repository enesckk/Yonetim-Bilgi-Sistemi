using PersonelYonetim.Domain.Common;
namespace PersonelYonetim.Domain.Entities;
public sealed class EventMedia : AuditableEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Kind { get; set; } = "photo";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Size { get; set; }
    public bool IncludeInReport { get; set; }
    public byte[] FileData { get; set; } = [];
}
