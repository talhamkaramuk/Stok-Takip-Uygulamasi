namespace STOKIO.Application.Dtos.Dashboard;

public sealed record DashboardSummaryDto(
    int ActiveProductCount,
    int ProductCount,
    int TotalStock,
    int CriticalStockCount,
    int CategoryCount,
    int CustomerCount,
    int ActiveCustomerCount,
    int SupplierCount,
    int ActiveSupplierCount,
    int WarehouseCount,
    int ActiveWarehouseCount,
    int UserCount,
    int ActiveUserCount,
    int StockMovementCount,
    int StockInMovementCount,
    int StockOutMovementCount,
    int CountCorrectionMovementCount,
    int OrderCount,
    int PendingOrderCount,
    int PartiallyShippedOrderCount,
    int ShippedOrderCount,
    int CancelledOrderCount,
    int PurchaseRequestCount,
    int PendingPurchaseRequestCount,
    int ApprovedPurchaseRequestCount,
    int PartiallyReceivedPurchaseRequestCount,
    int ReceivedPurchaseRequestCount,
    int ShipmentCount,
    int CompletedShipmentCount,
    int CancelledShipmentCount,
    int ReturnCount,
    int ReceivedReturnCount,
    int RejectedReturnCount,
    IReadOnlyList<DashboardTrendPointDto> OperationTrend,
    IReadOnlyList<DashboardStockFlowPointDto> StockFlow,
    IReadOnlyList<DashboardBarDto> OperationBars,
    IReadOnlyList<DashboardJobDto> PendingJobs,
    IReadOnlyList<DashboardBarDto> WarehouseBars,
    IReadOnlyList<DashboardProductRankDto> TopProducts,
    IReadOnlyList<DashboardRecentOperationDto> RecentOperations);

public sealed record DashboardTrendPointDto(string Label, int Total);

public sealed record DashboardStockFlowPointDto(string Label, int Inbound, int Outbound);

public sealed record DashboardBarDto(string Label, int Value, string? Tone = null);

public sealed record DashboardJobDto(string Label, int Value);

public sealed record DashboardProductRankDto(Guid ProductId, string Sku, string ProductName, int Quantity);

public sealed record DashboardRecentOperationDto(
    Guid Id,
    string Type,
    string Number,
    string Party,
    int Quantity,
    string Status,
    DateTimeOffset Date);
