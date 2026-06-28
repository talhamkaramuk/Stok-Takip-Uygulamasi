using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class SalesOrder : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Pending;
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public List<SalesOrderItem> Items { get; } = [];
}
