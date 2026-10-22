using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Stock;

public sealed class StockCustodyDto
{
    public Guid Id { get; init; }
    public Guid StockItemId { get; init; }
    public string ItemName { get; init; } = "";
    public string? ItemCode { get; init; }
    public string UnitLabel { get; init; } = "";
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = "";
    public string? EmployeeNumber { get; init; }
    public decimal Quantity { get; init; }
    public string? SerialNumber { get; init; }
    public DateOnly AssignedOn { get; init; }
    public DateOnly? ReturnedOn { get; init; }
    public string? Notes { get; init; }
    public string? ReturnNotes { get; init; }
    public Guid? ReplacesId { get; init; }
}

public sealed record GetStockCustodiesQuery(Guid? EmployeeId = null, bool IncludeReturned = false)
    : IRequest<IReadOnlyList<StockCustodyDto>>;
public sealed record CustodyEmployeeDto(Guid Id, string Name, string? EmployeeNumber);
public sealed record GetCustodyEmployeesQuery : IRequest<IReadOnlyList<CustodyEmployeeDto>>;

public sealed class AssignStockCustodyCommand : IRequest<StockCustodyDto>
{
    // Client-generated key also makes retries after a lost response idempotent.
    public Guid Id { get; set; }
    public Guid StockItemId { get; set; }
    public Guid LocationId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal Quantity { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly AssignedOn { get; set; }
    public string? Notes { get; set; }
    public Guid? ReplacesId { get; set; }
}

public sealed class AssignStockCustodyValidator : AbstractValidator<AssignStockCustodyCommand>
{
    public AssignStockCustodyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.StockItemId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(400);
        RuleFor(x => x.AssignedOn).NotEmpty().LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)));
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.SerialNumber) || x.Quantity == 1)
            .WithMessage("Seri numaralı malzemede miktar 1 olmalıdır.");
        RuleFor(x => x.ReplacesId).NotEqual(Guid.Empty).When(x => x.ReplacesId.HasValue);
    }
}

public sealed class ReturnStockCustodyCommand : IRequest<StockCustodyDto>
{
    public Guid Id { get; set; }
    public DateOnly ReturnedOn { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReturnStockCustodyValidator : AbstractValidator<ReturnStockCustodyCommand>
{
    public ReturnStockCustodyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ReturnedOn).NotEmpty().LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)));
        RuleFor(x => x.Notes).MaximumLength(400);
    }
}
