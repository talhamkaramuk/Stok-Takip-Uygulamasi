using STOKIO.Application.Common;

namespace STOKIO.Application.Dtos.Warehouses;

public sealed record CreateWarehouseRequest(
    string Code,
    string Name,
    string? Address,
    bool IsDefault);

public sealed record UpdateWarehouseRequest(
    string Code,
    string Name,
    string? Address,
    bool IsDefault,
    bool IsActive);

public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    bool IsDefault,
    bool IsActive,
    int ProductCount,
    int TotalQuantity);

public sealed record WarehouseStockDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string Sku,
    string ProductName,
    int Quantity,
    int CriticalStockLevel,
    bool IsCritical);

public sealed record StockTransferRequest(
    Guid ProductId,
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    int Quantity,
    string? Reason);

public sealed record StockTransferDto(
    Guid TransferGroupId,
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid FromWarehouseId,
    string FromWarehouseName,
    Guid ToWarehouseId,
    string ToWarehouseName,
    int Quantity,
    DateTimeOffset CreatedAt);
