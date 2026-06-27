using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class ProductBarcode : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Barcode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

