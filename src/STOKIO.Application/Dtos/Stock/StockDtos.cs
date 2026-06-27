using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Stock;

public sealed record CreateStockMovementRequest(
    Guid ProductId,
    StockMovementType Type,
    int Quantity,
    string? Reason,
    Guid? WarehouseId = null);

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    Guid? WarehouseId,
    string? WarehouseName,
    StockMovementType Type,
    int Quantity,
    int PreviousQuantity,
    int NewQuantity,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record CriticalStockDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int CurrentStock,
    int CriticalStockLevel);

public sealed record StockConsistencyDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int StoredCurrentStock,
    int LedgerCurrentStock,
    bool IsConsistent,
    IReadOnlyList<string> Issues);
