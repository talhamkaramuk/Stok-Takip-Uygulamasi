using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class ShipmentItem : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
}
