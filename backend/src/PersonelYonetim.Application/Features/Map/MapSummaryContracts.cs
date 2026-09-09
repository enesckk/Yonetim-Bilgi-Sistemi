using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Map;

public sealed class SettlementSummaryDto
{
    public Guid SettlementId { get; init; }
    public string OfficialCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string SettlementType { get; init; } = string.Empty;
    public bool IsRural { get; init; }
    public double? CentroidLat { get; init; }
    public double? CentroidLng { get; init; }
    public int? Population { get; init; }
    public int? MaleCount { get; init; }
    public int? FemaleCount { get; init; }
    public int? ChildCount { get; init; }
    public int? PopulationYear { get; init; }
    public string? PopulationSource { get; init; }
    public bool PopulationIsOfficial { get; init; }
    public int ActivityCount { get; init; }
    public int AttendanceCount { get; init; }
    public int? UniqueBeneficiaryCount { get; init; }
    /// <summary>true ise unique / nüfus; değilse katılım / nüfus (yoğunluk).</summary>
    public bool HasUniqueBeneficiaries { get; init; }
    public decimal? CoverageRate { get; init; }
    public string CoverageLevel { get; init; } = "none";
    public string MetricLabel { get; init; } = string.Empty;
    public DateTime? LastActivityDate { get; init; }
    public string? HeadmanName { get; init; }
    public string? HeadmanPhone { get; init; }
    public IReadOnlyList<SettlementSchoolDto> Schools { get; set; } = [];
    public IReadOnlyList<SettlementFacilityDto> Facilities { get; set; } = [];
    public IReadOnlyList<SettlementAreaDto> Areas { get; set; } = [];
}

public sealed class SettlementFacilityDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public int Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? ManagerName { get; init; }
    public int? Capacity { get; init; }
    public string? WorkingHours { get; init; }
}

public sealed class SettlementAreaDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AreaType { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string? Address { get; init; }
}

public sealed class SettlementSchoolDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SchoolType { get; init; } = string.Empty;
    public int? StudentCount { get; init; }
    public string? PrincipalName { get; init; }
    public string? PrincipalPhone { get; init; }
}

public sealed class SettlementSummaryListDto
{
    public IReadOnlyList<SettlementSummaryDto> Items { get; init; } = [];
}

public sealed record GetSettlementSummariesQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Category = null,
    EventStatus? Status = null,
    string? Search = null) : IRequest<SettlementSummaryListDto>;

public sealed class SettlementLookupDto
{
    public Guid Id { get; init; }
    public string OfficialCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsRural { get; init; }
    public string? HeadmanName { get; init; }
    public string? HeadmanPhone { get; init; }
}

public sealed record GetSettlementLookupsQuery(string? Search = null)
    : IRequest<IReadOnlyList<SettlementLookupDto>>;

public sealed record GetSettlementDetailQuery(string Key) : IRequest<SettlementSummaryDto>;

public sealed class UpsertSettlementPopulationCommand : IRequest
{
    public Guid SettlementId { get; set; }
    public int Year { get; set; }
    public int Population { get; set; }
    public int? MaleCount { get; set; }
    public int? FemaleCount { get; set; }
    public int? ChildCount { get; set; }
    public string Source { get; set; } = "manual";
    public string? SourceReference { get; set; }
    public bool IsOfficial { get; set; }
}

public sealed class UpsertSettlementPopulationValidator : AbstractValidator<UpsertSettlementPopulationCommand>
{
    public UpsertSettlementPopulationValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(1990, 2100);
        RuleFor(x => x.Population).InclusiveBetween(1, 5_000_000);
        RuleFor(x => x.MaleCount).InclusiveBetween(0, 5_000_000).When(x => x.MaleCount.HasValue);
        RuleFor(x => x.FemaleCount).InclusiveBetween(0, 5_000_000).When(x => x.FemaleCount.HasValue);
        RuleFor(x => x.ChildCount).InclusiveBetween(0, 5_000_000).When(x => x.ChildCount.HasValue);
        RuleFor(x => x)
            .Must(x =>
            {
                if (x.MaleCount is null && x.FemaleCount is null && x.ChildCount is null)
                    return true;
                var sum = (x.MaleCount ?? 0) + (x.FemaleCount ?? 0) + (x.ChildCount ?? 0);
                return sum == x.Population;
            })
            .WithMessage("Çocuk, kadın ve erkek toplamı nüfusa eşit olmalıdır.");
        RuleFor(x => x.Source).NotEmpty().MaximumLength(80);
        RuleFor(x => x.SourceReference).MaximumLength(300);
    }
}

public sealed class UpdateSettlementProfileCommand : IRequest
{
    public Guid SettlementId { get; set; }
    public string? HeadmanName { get; set; }
    public string? HeadmanPhone { get; set; }
}

public sealed class UpdateSettlementProfileValidator : AbstractValidator<UpdateSettlementProfileCommand>
{
    public UpdateSettlementProfileValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
        RuleFor(x => x.HeadmanName).MaximumLength(120);
        RuleFor(x => x.HeadmanPhone).MaximumLength(30);
    }
}

public sealed class UpsertSettlementSchoolCommand : IRequest<Guid>
{
    public Guid SettlementId { get; set; }
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchoolType { get; set; } = "İlkokul";
    public int? StudentCount { get; set; }
    public string? PrincipalName { get; set; }
    public string? PrincipalPhone { get; set; }
}

public sealed class UpsertSettlementSchoolValidator : AbstractValidator<UpsertSettlementSchoolCommand>
{
    public UpsertSettlementSchoolValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SchoolType).NotEmpty().MaximumLength(40);
        RuleFor(x => x.StudentCount).InclusiveBetween(0, 50_000).When(x => x.StudentCount.HasValue);
        RuleFor(x => x.PrincipalName).MaximumLength(120);
        RuleFor(x => x.PrincipalPhone).MaximumLength(30);
    }
}

public sealed class DeleteSettlementSchoolCommand : IRequest
{
    public Guid SettlementId { get; set; }
    public Guid SchoolId { get; set; }
}

public sealed class UpsertSettlementAreaCommand : IRequest<Guid>
{
    public Guid SettlementId { get; set; }
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AreaType { get; set; } = "Park";
    public string? Note { get; set; }
    public string? Address { get; set; }
}

public sealed class UpsertSettlementAreaValidator : AbstractValidator<UpsertSettlementAreaCommand>
{
    public UpsertSettlementAreaValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AreaType).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Note).MaximumLength(300);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class DeleteSettlementAreaCommand : IRequest
{
    public Guid SettlementId { get; set; }
    public Guid AreaId { get; set; }
}

public static class CoverageScale
{
    public const string None = "none";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string Good = "good";

    /// <summary>
    /// %20 altı kırmızı, %20–50 sarı, %50 üzeri yeşil. Etkinlik yok = none.
    /// </summary>
    public static string Level(int activityCount, decimal? ratePercent) =>
        activityCount <= 0 ? None
        : ratePercent is null || ratePercent < 20 ? Low
        : ratePercent < 50 ? Medium
        : Good;
}
