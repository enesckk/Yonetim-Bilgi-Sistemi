using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

public sealed class EmployeeDetailDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    /// <summary>Ham dosya yolu dönülmez; görüntüleme GET /api/employees/{id}/photo ile.</summary>
    public bool HasPhoto { get; init; }
    public EmployeeStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public byte ProfileCompletionPercent { get; init; }

    public EmployeeGeneralInfoDto General { get; init; } = new();
    public EmployeeCorporateInfoDto Corporate { get; init; } = new();
    public IReadOnlyList<EmployeeAssignmentDto> Assignments { get; init; } = [];
    public IReadOnlyList<EmployeeEducationDto> Education { get; init; } = [];
    public IReadOnlyList<EmployeeSkillDto> Skills { get; init; } = [];
    public IReadOnlyList<EmployeeCertificateDto> Certificates { get; init; } = [];
    public IReadOnlyList<EmployeeMovementDto> Movements { get; init; } = [];
    public IReadOnlyList<EmployeeNoteDto> Notes { get; init; } = [];

    /// <summary>Kayıt var mı? Detay ayrı yetkiye bağlı.</summary>
    public bool HasSpecialCondition { get; init; }
    public IReadOnlyList<EmployeeSpecialConditionDto>? SpecialConditions { get; init; }
}

public sealed class EmployeeGeneralInfoDto
{
    public DateOnly? BirthDate { get; init; }
    public string GenderLabel { get; init; } = string.Empty;

    public string? PersonalPhone { get; init; }
    public string? CorporatePhone { get; init; }
    public string? PersonalEmail { get; init; }
    public string? CorporateEmail { get; init; }

    /// <summary>Yetki yoksa null; varsa maskeli veya tam değer.</summary>
    public string? Address { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }

    /// <summary>Yetki yoksa maskeli (••••••1234) veya null.</summary>
    public string? NationalIdDisplay { get; init; }
    public bool NationalIdIsMasked { get; init; }
}

public sealed class EmployeeCorporateInfoDto
{
    public Guid? UnitId { get; init; }
    public string? DirectorateName { get; init; }
    public string? MainUnitName { get; init; }
    public string? SubUnitName { get; init; }
    public string? UnitName { get; init; }
    public Guid? FacilityId { get; init; }
    public string? FacilityName { get; init; }
    public string? EmploymentTypeName { get; init; }
    public string? JobTitleName { get; init; }
    public string? PrimaryDutyName { get; init; }
    public string? PrimaryDutyCategoryLabel { get; init; }
    public string? ManagerName { get; init; }
    public string? UnitSupervisorName { get; init; }

    public DateOnly? HireDate { get; init; }
    public DateOnly? DirectorateStartDate { get; init; }
    public DateOnly? UnitStartDate { get; init; }
    public DateOnly? DutyStartDate { get; init; }
}

public sealed class EmployeeAssignmentDto
{
    public Guid Id { get; init; }
    public Guid JobDutyId { get; init; }
    public string DutyName { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Description { get; init; }
}

public sealed class EmployeeEducationDto
{
    public Guid Id { get; init; }
    public EducationLevel Level { get; init; }
    public string LevelLabel { get; init; } = string.Empty;
    public string? University { get; init; }
    public string? Faculty { get; init; }
    public string? School { get; init; }
    public string? Department { get; init; }
    public string? Program { get; init; }
    public short? GraduationYear { get; init; }
    public EducationCompletionStatus CompletionStatus { get; init; }
    public string CompletionStatusLabel { get; init; } = string.Empty;
    public string? DiplomaNumber { get; init; }
    public string? Description { get; init; }
}

public sealed class EmployeeSkillDto
{
    public Guid Id { get; init; }
    public Guid SkillId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public SkillLevel Level { get; init; }
    public string LevelLabel { get; init; } = string.Empty;
    public string? ExperienceDuration { get; init; }
    public bool HasCertificate { get; init; }
    public DateOnly? CertificateDate { get; init; }
    public string? CertificateIssuer { get; init; }
    public string? Description { get; init; }
}

public sealed class EmployeeCertificateDto
{
    public Guid Id { get; init; }
    public Guid? CertificateDefinitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Issuer { get; init; }
    public string? Category { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public string? DocumentNumber { get; init; }
    public string? Description { get; init; }
    public Guid? RelatedSkillId { get; init; }
    public string? RelatedSkillName { get; init; }
}

public sealed class EmployeeMovementDto
{
    public Guid Id { get; init; }
    public MovementType MovementType { get; init; }
    public string MovementTypeLabel { get; init; } = string.Empty;
    public Guid? OldUnitId { get; init; }
    public string? OldUnitName { get; init; }
    public Guid? NewUnitId { get; init; }
    public string? NewUnitName { get; init; }
    public Guid? OldFacilityId { get; init; }
    public string? OldFacilityName { get; init; }
    public Guid? NewFacilityId { get; init; }
    public string? NewFacilityName { get; init; }
    public Guid? OldJobTitleId { get; init; }
    public string? OldJobTitleName { get; init; }
    public Guid? NewJobTitleId { get; init; }
    public string? NewJobTitleName { get; init; }
    public Guid? OldJobDutyId { get; init; }
    public string? OldJobDutyName { get; init; }
    public Guid? NewJobDutyId { get; init; }
    public string? NewJobDutyName { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Reason { get; init; }
    public string? Description { get; init; }
    public string? ApprovedBy { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed class EmployeeSpecialConditionDto
{
    public Guid Id { get; init; }
    public string ConditionType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsPermanent { get; init; }
    public bool RequiresDutyAdjustment { get; init; }
    public bool RequiresWorkspaceAdjustment { get; init; }
    public bool HasDocument { get; init; }
}

public sealed class EmployeeNoteDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public NoteCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime NoteDateUtc { get; init; }
    public NoteVisibility Visibility { get; init; }
    public string VisibilityLabel { get; init; } = string.Empty;
    public DateOnly? ReminderDate { get; init; }
    public string? CreatedBy { get; init; }
    public bool CanModify { get; init; }
}
