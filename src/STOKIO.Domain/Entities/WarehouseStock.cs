using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class WarehouseStock : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int Version { get; set; } = 1;
}
