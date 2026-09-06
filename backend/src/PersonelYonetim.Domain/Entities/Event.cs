using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>
/// Belediye / müdürlük etkinliği. Konum tesis üzerinden veya doğrudan koordinat ile verilir.
/// </summary>
public class Event : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    /// <summary>Beklenen / planlanan katılımcı sayısı (kapasite bilgisi).</summary>
    public int? ExpectedAttendees { get; set; }

    /// <summary>Tekrarlayan serinin ortak kimliği (tek etkinlikte null).</summary>
    public Guid? SeriesId { get; set; }

    public EventRecurrenceFrequency RecurrenceFrequency { get; set; } = EventRecurrenceFrequency.None;

    /// <summary>Etkinlikten sorumlu personel.</summary>
    public Guid? ResponsibleEmployeeId { get; set; }
    public Employee? ResponsibleEmployee { get; set; }

    /// <summary>Düzenleyen birim (ana/alt birim vb.).</summary>
    public Guid? OrganizingUnitId { get; set; }
    public OrganizationUnit? OrganizingUnit { get; set; }

    /// <summary>Bağlı tesis (varsa harita pin'i tesis koordinatından da okunabilir).</summary>
    public Guid? FacilityId { get; set; }
    public OrganizationUnit? Facility { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
}
