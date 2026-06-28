using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Counts;

public sealed record CreateInventoryCountRequest(string Name, Guid? WarehouseId = null);

public sealed record ScanCountItemRequest(string Barcode, int Quantity);

public sealed record CloseInventoryCountRequest(bool ApplyDifferences);

public sealed record InventoryCountDto(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    string? WarehouseName,
    InventoryCountStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt,
    int ItemCount,
    int DifferenceCount,
    bool HasPostSnapshotMovements,
    int PostSnapshotMovementCount,
    DateTimeOffset? LastPostSnapshotMovementAt);

public sealed record InventoryCountItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int ExpectedQuantity,
    int CountedQuantity,
    int Difference);

public sealed record InventoryCountDifferenceDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int ExpectedQuantity,
    int CountedQuantity,
    int Difference);
