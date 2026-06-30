using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class ReturnRequest : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Received;
    public DateTimeOffset ReceivedAt { get; set; }
    public List<ReturnRequestItem> Items { get; } = [];
}
