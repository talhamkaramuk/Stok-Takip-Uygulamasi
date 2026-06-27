using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class Customer : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<SalesOrder> SalesOrders { get; } = [];
    public List<Shipment> Shipments { get; } = [];
    public List<ReturnRequest> ReturnRequests { get; } = [];
}
