using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Movements;

public sealed class GetMovementListQuery : IRequest<MovementListResultDto>
{
    public string? Search { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public MovementType? MovementType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Take { get; set; } = 200;
}

public sealed class ExportMovementListExcelQuery : IRequest<MovementExportFileDto>
{
    public string? Search { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public MovementType? MovementType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class MovementExportFileDto
{
    public byte[] Content { get; init; } = [];
    public string FileName { get; init; } = "gorev-gecmisi.xlsx";
    public string ContentType { get; init; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed class MovementListResultDto
{
    public int TotalCount { get; init; }
    public int ReturnedCount { get; init; }
    public IReadOnlyList<MovementTypeStatDto> TypeStats { get; init; } = [];
    public IReadOnlyList<MovementListItemDto> Items { get; init; } = [];
    public IReadOnlyList<MovementTypeOptionDto> MovementTypes { get; init; } = [];
}

public sealed class MovementTypeStatDto
{
    public int MovementType { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class MovementTypeOptionDto
{
    public int Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class MovementListItemDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    public string? CurrentUnitName { get; init; }
    public int MovementType { get; init; }
    public string MovementTypeLabel { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? OldUnitName { get; init; }
    public string? NewUnitName { get; init; }
    public string? OldFacilityName { get; init; }
    public string? NewFacilityName { get; init; }
    public string? OldJobTitleName { get; init; }
    public string? NewJobTitleName { get; init; }
    public string? OldJobDutyName { get; init; }
    public string? NewJobDutyName { get; init; }
    public string? Reason { get; init; }
    public string? Description { get; init; }
    public string? ApprovedBy { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class GetMovementListQueryValidator : AbstractValidator<GetMovementListQuery>
{
    public GetMovementListQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}
