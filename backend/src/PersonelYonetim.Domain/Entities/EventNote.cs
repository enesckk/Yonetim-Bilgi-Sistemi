using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Etkinlik / toplantı saha notu. İstenirse mesaj olarak iletilir.</summary>
public class EventNote : AuditableEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public AppUser Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
}
