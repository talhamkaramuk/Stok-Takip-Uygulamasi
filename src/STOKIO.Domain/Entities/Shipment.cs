using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class Shipment : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string ShipmentNumber { get; set; } = string.Empty;
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Completed;
    public DateTimeOffset ShippedAt { get; set; }
    public string? Notes { get; set; }
    public List<ShipmentItem> Items { get; } = [];
}
