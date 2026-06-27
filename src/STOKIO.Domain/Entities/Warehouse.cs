using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class Warehouse : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public List<WarehouseStock> Stocks { get; } = [];
}
