using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Zimmet fiziksel stoğun bir bölümünü ayırır; iadede miktar tekrar kullanılabilir olur.</summary>
public class StockCustody : AuditableEntity
{
    public Guid StockItemId { get; set; }
    public StockItem StockItem { get; set; } = null!;
    public Guid LocationId { get; set; }
    public OrganizationUnit Location { get; set; } = null!;
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public decimal Quantity { get; set; }
    public StockUnit Unit { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly AssignedOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public string? Notes { get; set; }
    public string? ReturnNotes { get; set; }
    public Guid? ReplacesId { get; set; }
    public StockCustody? Replaces { get; set; }
}
