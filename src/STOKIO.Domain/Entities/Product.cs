using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class Product : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CriticalStockLevel { get; set; }
    public int CurrentStock { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public List<ProductBarcode> Barcodes { get; } = [];
    public List<WarehouseStock> WarehouseStocks { get; } = [];
}
