using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class PurchaseRequest : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string RequestNumber { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.PendingApproval;
    public string? Notes { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public ApplicationUser? RequestedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public List<PurchaseRequestItem> Items { get; } = [];
}
