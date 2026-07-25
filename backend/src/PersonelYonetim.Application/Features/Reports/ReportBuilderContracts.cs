using FluentValidation;
using MediatR;
using PersonelYonetim.Application.Features.Employees;

namespace PersonelYonetim.Application.Features.Reports;

/// <summary>
/// Rapor oluşturucu — kullanıcı sütun/filtre/sıralama/gruplama seçer,
/// sistem tabloyu üretir. Filtreler EmployeeListQuery ile aynıdır (parity).
/// </summary>
public class ReportBuildRequest : EmployeeListQuery
{
    public string? Title { get; set; }

    /// <summary>Seçilen sütun anahtarları (boşsa varsayılan set kullanılır).</summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>Gruplama yapılacak sütun anahtarı.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Sıralama yapılacak sütun anahtarı (boşsa soyad/ad).</summary>
    public string? OrderByColumn { get; set; }
    public bool OrderByDesc { get; set; }

    // Rapor oluşturucuya özel ek filtreler
    /// <summary>Sertifikası olan personeller (MissingCertificatesOnly'nin tersi).</summary>
    public bool HasCertificatesOnly { get; set; }

    /// <summary>Görev yeri / görev değişikliği (hareket) kaydı olan personeller.</summary>
    public bool HasMovementsOnly { get; set; }
    public DateOnly? MovementFrom { get; set; }
    public DateOnly? MovementTo { get; set; }
}

public sealed class BuildReportQuery : ReportBuildRequest, IRequest<ReportResultDto>;

public sealed class BuildReportExcelQuery : ReportBuildRequest, IRequest<ReportFileResult>;

public sealed class BuildReportPdfQuery : ReportBuildRequest, IRequest<ReportFileResult>;

/// <summary>Kullanıcının yetkisine göre seçilebilir rapor sütunları.</summary>
public sealed record GetReportColumnsQuery : IRequest<IReadOnlyList<ReportColumnDto>>;

public sealed class ReportColumnDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    /// <summary>UI'da gruplu gösterim için: Kimlik, Görev, İletişim, Eğitim…</summary>
    public string Group { get; init; } = string.Empty;
}

public sealed class ReportResultDto
{
    public string Title { get; init; } = string.Empty;
    public string GeneratedBy { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ReportColumnDto> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = [];
    public IReadOnlyList<string> AppliedFilters { get; init; } = [];
    /// <summary>Gruplama sütununun etiketi (varsa). Satırlar grup sırasına göre dizilidir.</summary>
    public string? GroupByKey { get; init; }
    public string? GroupByLabel { get; init; }
}

public sealed class BuildReportQueryValidator : AbstractValidator<BuildReportQuery>
{
    public BuildReportQueryValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Columns).Must(c => c.Count <= 30)
            .WithMessage("En fazla 30 sütun seçilebilir.");
    }
}

public sealed class BuildReportExcelQueryValidator : AbstractValidator<BuildReportExcelQuery>
{
    public BuildReportExcelQueryValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Columns).Must(c => c.Count <= 30)
            .WithMessage("En fazla 30 sütun seçilebilir.");
    }
}

public sealed class BuildReportPdfQueryValidator : AbstractValidator<BuildReportPdfQuery>
{
    public BuildReportPdfQueryValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Columns).Must(c => c.Count <= 12)
            .WithMessage("PDF çıktısında en fazla 12 sütun seçilebilir (sayfa genişliği).");
    }
}
