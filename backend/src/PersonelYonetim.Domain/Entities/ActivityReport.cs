using PersonelYonetim.Domain.Common;
namespace PersonelYonetim.Domain.Entities;
public sealed class ActivityReport : AuditableEntity
{
    public string Title { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string DateBasis { get; set; } = "event";
    public string Kind { get; set; } = "all";
    public int Revision { get; set; } = 1;
    public int SourceCount { get; set; }
    public long SourceAttendance { get; set; }
    public string BodyJson { get; set; } = "";
}
public sealed class ActivityReportAsset : AuditableEntity
{
    public Guid ReportId { get; set; }
    public ActivityReport Report { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] FileData { get; set; } = [];
    public Guid? SourceMediaId { get; set; }
}
