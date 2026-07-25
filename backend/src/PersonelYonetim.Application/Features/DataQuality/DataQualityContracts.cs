using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.DataQuality;

/// <summary>
/// Veri kalitesi = kayıtların ne kadar "tam ve tutarlı" olduğu.
/// ProfileCompletionPercent skoru; bu rapor eksikleri ve tutarsızlıkları eyleme dönüştürür.
/// </summary>
public sealed class GetDataQualityReportQuery : IRequest<DataQualityReportDto>
{
    /// <summary>Varsayılan: yalnızca Active. null = tüm durumlar.</summary>
    public EmployeeStatus? Status { get; set; } = EmployeeStatus.Active;

    /// <summary>Belirli bir eksik/tutarsızlık koduna filtre.</summary>
    public string? IssueCode { get; set; }

    /// <summary>missing | inconsistency — tür filtresi.</summary>
    public string? Kind { get; set; }

    /// <summary>Tamamlanma oranı üst sınırı (örn. 80 → %80 altı).</summary>
    public byte? MaxCompletionPercent { get; set; }

    /// <summary>Tamamlanma bandı: full | high | mid | low | critical | minimal</summary>
    public string? CompletionBand { get; set; }
}

public sealed class DataQualityReportDto
{
    public int ScopedEmployeeCount { get; init; }
    public int EmployeesWithIssues { get; init; }
    public int MissingIssueEmployeeCount { get; init; }
    public int InconsistencyEmployeeCount { get; init; }
    public double AverageCompletionPercent { get; init; }
    public IReadOnlyList<DataQualityCompletionBandDto> CompletionBands { get; init; } = [];
    public IReadOnlyList<DataQualityIssueStatDto> IssueStats { get; init; } = [];
    public IReadOnlyList<DataQualityEmployeeDto> Employees { get; init; } = [];
    public IReadOnlyList<DataQualityRuleDto> Rules { get; init; } = [];
}

public sealed class DataQualityCompletionBandDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public byte MinPercent { get; init; }
    public byte MaxPercent { get; init; }
    public int Count { get; init; }
}

public sealed class DataQualityIssueStatDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    /// <summary>missing | inconsistency</summary>
    public string Kind { get; init; } = "missing";
    public int Count { get; init; }
}

public sealed class DataQualityEmployeeDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    public string? UnitName { get; init; }
    public string? FacilityName { get; init; }
    public EmployeeStatus Status { get; init; }
    public byte ProfileCompletionPercent { get; init; }
    public IReadOnlyList<string> IssueCodes { get; init; } = [];
    public IReadOnlyList<string> IssueLabels { get; init; } = [];
    public IReadOnlyList<string> MissingCodes { get; init; } = [];
    public IReadOnlyList<string> InconsistencyCodes { get; init; } = [];
}

public sealed class DataQualityRuleDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>missing | inconsistency</summary>
    public string Kind { get; init; } = "missing";
    /// <summary>ProfileCompletionPercent hesabına giriyor mu?</summary>
    public bool AffectsCompletionScore { get; init; }
}

public static class DataQualityIssueKinds
{
    public const string Missing = "missing";
    public const string Inconsistency = "inconsistency";
}

/// <summary>Eksik alan ve tutarsızlık kodları — UI ve API aynı string'i kullanır.</summary>
public static class DataQualityIssueCodes
{
    // —— Eksik alanlar ——
    public const string MissingEmployeeNumber = "MissingEmployeeNumber";
    public const string MissingBirthDate = "MissingBirthDate";
    public const string MissingGender = "MissingGender";
    public const string MissingPhone = "MissingPhone";
    public const string MissingEmail = "MissingEmail";
    public const string MissingAddress = "MissingAddress";
    public const string MissingUnit = "MissingUnit";
    public const string MissingFacility = "MissingFacility";
    public const string MissingEmploymentType = "MissingEmploymentType";
    public const string MissingJobTitle = "MissingJobTitle";
    public const string MissingHireDate = "MissingHireDate";
    public const string MissingNationalId = "MissingNationalId";
    public const string MissingPrimaryAssignment = "MissingPrimaryAssignment";
    public const string MissingEducation = "MissingEducation";
    public const string MissingSkills = "MissingSkills";
    public const string MissingEmergencyContact = "MissingEmergencyContact";

    // —— Tutarsızlıklar ——
    public const string DuplicatePhone = "DuplicatePhone";
    public const string DuplicateEmail = "DuplicateEmail";
    public const string FutureHireDate = "FutureHireDate";
    public const string UnitStartBeforeHire = "UnitStartBeforeHire";
    public const string ActiveInPassiveFacility = "ActiveInPassiveFacility";
    public const string InactiveUnit = "InactiveUnit";
    public const string MultiplePrimaryDuties = "MultiplePrimaryDuties";
    public const string ActiveMissingFacility = "ActiveMissingFacility";
    public const string EmptyDutyCategory = "EmptyDutyCategory";
}
