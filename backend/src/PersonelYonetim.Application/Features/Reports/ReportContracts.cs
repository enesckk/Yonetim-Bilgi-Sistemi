using MediatR;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Reports;

/// <summary>
/// Personel listesini Excel'e aktar — Import'un tersi.
/// Sayfalama yok; üst sınır var. Alanlar yetkiye göre sütun olarak çıkar/çıkmaz.
/// Filtreler EmployeeListQuery ile aynıdır (liste ↔ export parity).
/// </summary>
public sealed class ExportEmployeesExcelQuery : EmployeeListQuery, IRequest<ReportFileResult>;

/// <summary>
/// Aynı filtrelerle PDF — yazdırma / resmi paylaşım için.
/// Excel = analiz/düzenleme; PDF = sabit belge (kolayca hücre düzenlenmez).
/// </summary>
public sealed class ExportEmployeesPdfQuery : EmployeeListQuery, IRequest<ReportFileResult>;

public sealed class ReportFileResult
{
    public required byte[] Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>Raporlar ana sayfası özeti — Reports.View.</summary>
public sealed record GetReportsSummaryQuery : IRequest<ReportsSummaryDto>;

public sealed class ReportsSummaryDto
{
    public int TotalEmployees { get; init; }
    public int ActiveEmployees { get; init; }
    public int PassiveEmployees { get; init; }
    public int OnLeaveEmployees { get; init; }
    public int WithSpecialCondition { get; init; }
    public int OrganizationUnitCount { get; init; }
    public IReadOnlyList<StatusCountDto> ByStatus { get; init; } = [];
}

public sealed class StatusCountDto
{
    public EmployeeStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public int Count { get; init; }
}
