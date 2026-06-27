using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Operations;

public sealed record OperationItemRequest(Guid ProductId, int Quantity);

public sealed record OperationItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int Quantity);

public sealed record CreateSalesOrderRequest(
    string CustomerName,
    Guid? WarehouseId,
    string? Notes,
    IReadOnlyList<OperationItemRequest> Items,
    Guid? CustomerId = null);

public sealed record SalesOrderDto(
    Guid Id,
    string OrderNumber,
    Guid? CustomerId,
    string CustomerName,
    Guid? WarehouseId,
    string? WarehouseName,
    SalesOrderStatus Status,
    int LineCount,
    int TotalQuantity,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationItemDto> Items);

public sealed record CreatePurchaseRequestRequest(
    string SupplierName,
    Guid? WarehouseId,
    string? Notes,
    IReadOnlyList<OperationItemRequest> Items,
    Guid? SupplierId = null);

public sealed record PurchaseRequestDto(
    Guid Id,
    string RequestNumber,
    Guid? SupplierId,
    string SupplierName,
    Guid? WarehouseId,
    string? WarehouseName,
    PurchaseRequestStatus Status,
    int LineCount,
    int TotalQuantity,
    string? Notes,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationItemDto> Items);

public sealed record CreateShipmentRequest(
    Guid? SalesOrderId,
    string RecipientName,
    Guid? WarehouseId,
    string? TrackingNumber,
    string? Notes,
    IReadOnlyList<OperationItemRequest> Items,
    Guid? CustomerId = null);

public sealed record ShipmentDto(
    Guid Id,
    string ShipmentNumber,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    Guid? CustomerId,
    string RecipientName,
    Guid? WarehouseId,
    string? WarehouseName,
    string? TrackingNumber,
    ShipmentStatus Status,
    int LineCount,
    int TotalQuantity,
    DateTimeOffset ShippedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationItemDto> Items);

public sealed record CreateReturnRequestRequest(
    Guid? SalesOrderId,
    string CustomerName,
    Guid? WarehouseId,
    string Reason,
    IReadOnlyList<OperationItemRequest> Items,
    Guid? CustomerId = null);

public sealed record ReturnRequestDto(
    Guid Id,
    string ReturnNumber,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    Guid? CustomerId,
    string CustomerName,
    Guid? WarehouseId,
    string? WarehouseName,
    string Reason,
    ReturnRequestStatus Status,
    int LineCount,
    int TotalQuantity,
    DateTimeOffset ReceivedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationItemDto> Items);
