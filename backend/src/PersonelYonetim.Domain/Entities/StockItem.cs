using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Müdürlük malzeme tanımı (katalog). Miktar yer bazında StockBalance'ta tutulur.</summary>
public class StockItem : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public StockCategory Category { get; set; } = StockCategory.Other;
    public StockUnit Unit { get; set; } = StockUnit.Piece;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public decimal MinQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<StockBalance> Balances { get; set; } = new List<StockBalance>();
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}

/// <summary>Bir malzemenin belirli tesisteki / birimdeki miktarı.</summary>
public class StockBalance : AuditableEntity
{
    public Guid StockItemId { get; set; }
    public StockItem? StockItem { get; set; }

    public Guid LocationId { get; set; }
    public OrganizationUnit? Location { get; set; }

    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class StockMovement : AuditableEntity
{
    public Guid StockItemId { get; set; }
    public StockItem? StockItem { get; set; }

    public StockMovementType MovementType { get; set; }

    public Guid? FromLocationId { get; set; }
    public OrganizationUnit? FromLocation { get; set; }

    public Guid? ToLocationId { get; set; }
    public OrganizationUnit? ToLocation { get; set; }

    public decimal Quantity { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string? Reason { get; set; }
}
