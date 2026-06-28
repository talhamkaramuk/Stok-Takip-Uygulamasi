using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class SalesOrderItem : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int ShippedQuantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public int Version { get; set; } = 1;
}
