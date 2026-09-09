using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Personel-personel bire bir dahili mesaj.</summary>
public class DirectMessage : AuditableEntity
{
    public Guid SenderUserId { get; set; }
    public AppUser Sender { get; set; } = null!;

    public Guid RecipientUserId { get; set; }
    public AppUser Recipient { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public DateTime? ReadAtUtc { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }

    public Guid? RelatedEventId { get; set; }
    public Event? RelatedEvent { get; set; }
}
