using PersonelYonetim.Domain.Common;
namespace PersonelYonetim.Domain.Entities;

// Source rows are retained individually: several legitimate rows can share a facility and month.
public sealed class FacilityUsageRecord : AuditableEntity
{
    public Guid FacilityId { get; set; }
    public OrganizationUnit Facility { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public long ParticipantCount { get; set; }
    public string SourceKey { get; set; } = "";
    public string SourceReference { get; set; } = "";
}
