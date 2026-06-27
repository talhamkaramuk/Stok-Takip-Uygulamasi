using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Reports;

public sealed record CurrentStockReportRow(
    Guid ProductId,
    string Sku,
    string ProductName,
    string? CategoryName,
    int CurrentStock,
    int CriticalStockLevel,
    bool IsCritical);

public sealed record MovementReportRow(
    Guid MovementId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string? WarehouseName,
    StockMovementType Type,
    int Quantity,
    int PreviousQuantity,
    int NewQuantity,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record CountDifferenceReportRow(
    Guid ProductId,
    string Sku,
    string ProductName,
    int ExpectedQuantity,
    int CountedQuantity,
    int Difference);
