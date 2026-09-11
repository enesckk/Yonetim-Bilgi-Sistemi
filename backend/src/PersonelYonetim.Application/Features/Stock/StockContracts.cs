using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Stock;

public sealed class StockLookupDto
{
    public int Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class StockLocationOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
}

public sealed class StockOptionsDto
{
    public IReadOnlyList<StockLookupDto> Categories { get; init; } = [];
    public IReadOnlyList<StockLookupDto> Units { get; init; } = [];
    public IReadOnlyList<StockLookupDto> MovementTypes { get; init; } = [];
    public IReadOnlyList<StockLocationOptionDto> Locations { get; init; } = [];
}

public sealed class StockItemListRowDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public StockCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public StockUnit Unit { get; init; }
    public string UnitLabel { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public decimal MinQuantity { get; init; }
    public decimal TotalQuantity { get; init; }
    public int LocationCount { get; init; }
    public IReadOnlyList<StockItemLocationBriefDto> Locations { get; init; } = [];
    public bool IsLow { get; init; }
    public bool IsActive { get; init; }
}

public sealed class StockItemLocationBriefDto
{
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

public sealed class StockBalanceLineDto
{
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string LocationTypeLabel { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal MinQuantity { get; init; }
    public bool IsLow { get; init; }
    public string? Notes { get; init; }
}

public sealed class StockItemDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public StockCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public StockUnit Unit { get; init; }
    public string UnitLabel { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? Description { get; init; }
    public decimal MinQuantity { get; init; }
    public decimal TotalQuantity { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<StockBalanceLineDto> Balances { get; init; } = [];
}

public sealed class StockLocationCardDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public int LowCount { get; init; }
}

public sealed class StockLocationLineDto
{
    public Guid StockItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public StockCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public StockUnit Unit { get; init; }
    public string UnitLabel { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public decimal Quantity { get; init; }
    public decimal MinQuantity { get; init; }
    public bool IsLow { get; init; }
}

public sealed class StockLocationDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public int LowCount { get; init; }
    public IReadOnlyList<StockLocationLineDto> Lines { get; init; } = [];
}

public sealed class StockMovementDto
{
    public Guid Id { get; init; }
    public Guid StockItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? ItemCode { get; init; }
    public string UnitLabel { get; init; } = string.Empty;
    public StockMovementType MovementType { get; init; }
    public string MovementTypeLabel { get; init; } = string.Empty;
    public Guid? FromLocationId { get; init; }
    public string? FromLocationName { get; init; }
    public Guid? ToLocationId { get; init; }
    public string? ToLocationName { get; init; }
    public decimal Quantity { get; init; }
    public DateOnly OccurredOn { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class StockLowLineDto
{
    public Guid StockItemId { get; init; }
    public Guid LocationId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string UnitLabel { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal MinQuantity { get; init; }
}

public sealed class StockSummaryDto
{
    public int CatalogCount { get; init; }
    public int LocationCount { get; init; }
    public int LocationsWithStock { get; init; }
    public int LowCount { get; init; }
    public IReadOnlyList<StockLocationCardDto> Locations { get; init; } = [];
    public IReadOnlyList<StockLowLineDto> LowStock { get; init; } = [];
    public IReadOnlyList<StockMovementDto> RecentMovements { get; init; } = [];
}

public sealed record GetStockOptionsQuery : IRequest<StockOptionsDto>;

public sealed record GetStockSummaryQuery : IRequest<StockSummaryDto>;

public sealed record GetStockItemsQuery(
    string? Search = null,
    StockCategory? Category = null,
    bool IncludeInactive = false) : IRequest<IReadOnlyList<StockItemListRowDto>>;

public sealed record GetStockItemQuery(Guid Id) : IRequest<StockItemDetailDto>;

public sealed record GetStockLocationQuery(Guid LocationId) : IRequest<StockLocationDetailDto>;

public sealed record GetStockMovementsQuery(
    Guid? StockItemId = null,
    Guid? LocationId = null,
    int Take = 80) : IRequest<IReadOnlyList<StockMovementDto>>;

public sealed class UpsertStockItemCommand : IRequest<StockItemDetailDto>
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public StockCategory Category { get; set; } = StockCategory.Other;
    public StockUnit Unit { get; set; } = StockUnit.Piece;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public decimal MinQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpsertStockItemValidator : AbstractValidator<UpsertStockItemCommand>
{
    public UpsertStockItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Malzeme adı girin.");
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.Brand).MaximumLength(80);
        RuleFor(x => x.Model).MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MinQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Unit).IsInEnum();
    }
}

public sealed record ArchiveStockItemCommand(Guid Id) : IRequest;

public sealed class CreateStockMovementCommand : IRequest<StockMovementDto>
{
    public Guid StockItemId { get; set; }
    public StockMovementType MovementType { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly? OccurredOn { get; set; }
    public string? Reason { get; set; }
}

public sealed class CreateStockMovementValidator : AbstractValidator<CreateStockMovementCommand>
{
    public CreateStockMovementValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty();
        RuleFor(x => x.MovementType).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Miktar sıfırdan büyük olmalı.");
        RuleFor(x => x.Reason).MaximumLength(400);
        RuleFor(x => x)
            .Must(x => x.ToLocationId.HasValue)
            .When(x => x.MovementType is StockMovementType.Inbound or StockMovementType.Adjustment)
            .WithMessage("Hedef yer seçin.");
        RuleFor(x => x)
            .Must(x => x.FromLocationId.HasValue)
            .When(x => x.MovementType == StockMovementType.Outbound)
            .WithMessage("Çıkış yerini seçin.");
        RuleFor(x => x)
            .Must(x => x.FromLocationId.HasValue && x.ToLocationId.HasValue && x.FromLocationId != x.ToLocationId)
            .When(x => x.MovementType == StockMovementType.Transfer)
            .WithMessage("Transfer için farklı kaynak ve hedef yer seçin.");
    }
}
