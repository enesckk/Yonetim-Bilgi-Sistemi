using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

public class EducationRecord : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public EducationLevel Level { get; set; }
    public string? University { get; set; }
    public string? Faculty { get; set; }
    public string? School { get; set; }
    public string? Department { get; set; }
    public string? Program { get; set; }
    public short? GraduationYear { get; set; }
    public EducationCompletionStatus CompletionStatus { get; set; } = EducationCompletionStatus.Unknown;
    public string? DiplomaNumber { get; set; }
    public string? Description { get; set; }
    public string? DocumentPath { get; set; }
}
