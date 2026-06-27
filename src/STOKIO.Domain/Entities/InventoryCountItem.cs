using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class InventoryCountItem : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid InventoryCountId { get; set; }
    public InventoryCount InventoryCount { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int ExpectedQuantity { get; set; }
    public int CountedQuantity { get; set; }
}

