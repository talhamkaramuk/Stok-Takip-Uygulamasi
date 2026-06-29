using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Dashboard;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class DashboardService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Select(x => new { x.Id, x.Sku, x.Name, x.IsActive, x.CurrentStock, x.CriticalStockLevel })
            .ToListAsync(cancellationToken);
        var stockMovementStats = await dbContext.StockMovements
            .AsNoTracking()
            .GroupBy(x => x.Type)
            .Select(x => new { Type = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var orderStats = await dbContext.SalesOrders
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var purchaseStats = await dbContext.PurchaseRequests
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var shipmentStats = await dbContext.Shipments
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var returnStats = await dbContext.ReturnRequests
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var activeProductCount = products.Count(x => x.IsActive);
        var criticalStockCount = products.Count(x => x.IsActive && x.CurrentStock <= x.CriticalStockLevel);
        var orderCount = orderStats.Sum(x => x.Count);
        var purchaseRequestCount = purchaseStats.Sum(x => x.Count);
        var shipmentCount = shipmentStats.Sum(x => x.Count);
        var returnCount = returnStats.Sum(x => x.Count);
        var stockMovementCount = stockMovementStats.Sum(x => x.Count);
        var pendingOrderCount = CountBy(orderStats, SalesOrderStatus.Pending, x => x.Status, x => x.Count);
        var partiallyShippedOrderCount = CountBy(orderStats, SalesOrderStatus.PartiallyShipped, x => x.Status, x => x.Count);
        var pendingPurchaseRequestCount = CountBy(purchaseStats, PurchaseRequestStatus.PendingApproval, x => x.Status, x => x.Count);
        var approvedPurchaseRequestCount = CountBy(purchaseStats, PurchaseRequestStatus.Approved, x => x.Status, x => x.Count);
        var partiallyReceivedPurchaseRequestCount = CountBy(purchaseStats, PurchaseRequestStatus.PartiallyReceived, x => x.Status, x => x.Count);

        var operationTrend = await BuildOperationTrendAsync(cancellationToken);
        var stockFlow = await BuildStockFlowAsync(cancellationToken);
        var warehouseBars = await BuildWarehouseBarsAsync(cancellationToken);
        var topProducts = await BuildTopProductsAsync(cancellationToken);
        var recentOperations = await BuildRecentOperationsAsync(cancellationToken);

        return new DashboardSummaryDto(
            activeProductCount,
            products.Count,
            products.Sum(x => x.CurrentStock),
            criticalStockCount,
            await dbContext.Categories.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.Customers.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.Customers.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken),
            await dbContext.Suppliers.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.Suppliers.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken),
            await dbContext.Warehouses.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.Warehouses.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken),
            await dbContext.ApplicationUsers.AsNoTracking().CountAsync(cancellationToken),
            await dbContext.ApplicationUsers.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken),
            stockMovementCount,
            CountBy(stockMovementStats, StockMovementType.In, x => x.Type, x => x.Count) + CountBy(stockMovementStats, StockMovementType.TransferIn, x => x.Type, x => x.Count),
            CountBy(stockMovementStats, StockMovementType.Out, x => x.Type, x => x.Count) + CountBy(stockMovementStats, StockMovementType.TransferOut, x => x.Type, x => x.Count),
            CountBy(stockMovementStats, StockMovementType.CountCorrection, x => x.Type, x => x.Count),
            orderCount,
            pendingOrderCount,
            partiallyShippedOrderCount,
            CountBy(orderStats, SalesOrderStatus.Shipped, x => x.Status, x => x.Count),
            CountBy(orderStats, SalesOrderStatus.Cancelled, x => x.Status, x => x.Count),
            purchaseRequestCount,
            pendingPurchaseRequestCount,
            approvedPurchaseRequestCount,
            partiallyReceivedPurchaseRequestCount,
            CountBy(purchaseStats, PurchaseRequestStatus.Received, x => x.Status, x => x.Count),
            shipmentCount,
            CountBy(shipmentStats, ShipmentStatus.Completed, x => x.Status, x => x.Count),
            CountBy(shipmentStats, ShipmentStatus.Cancelled, x => x.Status, x => x.Count),
            returnCount,
            CountBy(returnStats, ReturnRequestStatus.Received, x => x.Status, x => x.Count),
            CountBy(returnStats, ReturnRequestStatus.Rejected, x => x.Status, x => x.Count),
            operationTrend,
            stockFlow,
            [
                new DashboardBarDto("Sipariş", orderCount, "primary"),
                new DashboardBarDto("Alım", purchaseRequestCount, "success"),
                new DashboardBarDto("Sevkiyat", shipmentCount, "info"),
                new DashboardBarDto("İade", returnCount, "warning")
            ],
            [
                new DashboardJobDto("Bekleyen sipariş", pendingOrderCount + partiallyShippedOrderCount),
                new DashboardJobDto("Onay bekleyen alım", pendingPurchaseRequestCount),
                new DashboardJobDto("Teslim alınacak alım", approvedPurchaseRequestCount + partiallyReceivedPurchaseRequestCount),
                new DashboardJobDto("Kritik stok", criticalStockCount)
            ],
            warehouseBars,
            topProducts,
            recentOperations);
    }

    private async Task<IReadOnlyList<DashboardTrendPointDto>> BuildOperationTrendAsync(CancellationToken cancellationToken)
    {
        var days = RecentDays(14);
        var start = days[0].Start;
        var counts = days.ToDictionary(x => x.Key, _ => 0);
        var dates = new List<DateTimeOffset>();
        dates.AddRange(await dbContext.SalesOrders.AsNoTracking().Where(x => x.CreatedAt >= start).Select(x => x.CreatedAt).ToListAsync(cancellationToken));
        dates.AddRange(await dbContext.PurchaseRequests.AsNoTracking().Where(x => x.CreatedAt >= start).Select(x => x.CreatedAt).ToListAsync(cancellationToken));
        dates.AddRange(await dbContext.Shipments.AsNoTracking().Where(x => x.ShippedAt >= start).Select(x => x.ShippedAt).ToListAsync(cancellationToken));
        dates.AddRange(await dbContext.ReturnRequests.AsNoTracking().Where(x => x.ReceivedAt >= start).Select(x => x.ReceivedAt).ToListAsync(cancellationToken));

        foreach (var date in dates)
        {
            var key = DateKey(date);
            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
        }

        return days.Select(x => new DashboardTrendPointDto(x.Label, counts[x.Key])).ToList();
    }

    private async Task<IReadOnlyList<DashboardStockFlowPointDto>> BuildStockFlowAsync(CancellationToken cancellationToken)
    {
        var days = RecentDays(14);
        var start = days[0].Start;
        var flow = days.ToDictionary(x => x.Key, _ => new StockFlowAccumulator());
        var movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start)
            .Select(x => new { x.CreatedAt, x.Type, x.Quantity, x.PreviousQuantity, x.NewQuantity })
            .ToListAsync(cancellationToken);

        foreach (var movement in movements)
        {
            var key = DateKey(movement.CreatedAt);
            if (!flow.TryGetValue(key, out var point))
            {
                continue;
            }

            if (movement.Type is StockMovementType.In or StockMovementType.TransferIn)
            {
                point.Inbound += movement.Quantity;
            }
            else if (movement.Type is StockMovementType.Out or StockMovementType.TransferOut)
            {
                point.Outbound += movement.Quantity;
            }
            else if (movement.NewQuantity > movement.PreviousQuantity)
            {
                point.Inbound += movement.NewQuantity - movement.PreviousQuantity;
            }
            else if (movement.PreviousQuantity > movement.NewQuantity)
            {
                point.Outbound += movement.PreviousQuantity - movement.NewQuantity;
            }
        }

        return days
            .Select(x => new DashboardStockFlowPointDto(x.Label, flow[x.Key].Inbound, flow[x.Key].Outbound))
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardBarDto>> BuildWarehouseBarsAsync(CancellationToken cancellationToken)
    {
        var stockByWarehouse = await dbContext.WarehouseStocks
            .AsNoTracking()
            .GroupBy(x => x.WarehouseId)
            .Select(x => new { WarehouseId = x.Key, Quantity = x.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);
        var quantities = stockByWarehouse.ToDictionary(x => x.WarehouseId, x => x.Quantity);
        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        return warehouses
            .Select(x => new DashboardBarDto(x.Name, quantities.GetValueOrDefault(x.Id)))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .Take(6)
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardProductRankDto>> BuildTopProductsAsync(CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, int>();
        AddTotals(totals, await dbContext.SalesOrderItems.AsNoTracking().GroupBy(x => x.ProductId).Select(x => new ProductQuantity(x.Key, x.Sum(i => i.Quantity))).ToListAsync(cancellationToken));
        AddTotals(totals, await dbContext.PurchaseRequestItems.AsNoTracking().GroupBy(x => x.ProductId).Select(x => new ProductQuantity(x.Key, x.Sum(i => i.Quantity))).ToListAsync(cancellationToken));
        AddTotals(totals, await dbContext.ShipmentItems.AsNoTracking().GroupBy(x => x.ProductId).Select(x => new ProductQuantity(x.Key, x.Sum(i => i.Quantity))).ToListAsync(cancellationToken));
        AddTotals(totals, await dbContext.ReturnRequestItems.AsNoTracking().GroupBy(x => x.ProductId).Select(x => new ProductQuantity(x.Key, x.Sum(i => i.Quantity))).ToListAsync(cancellationToken));

        var productIds = totals
            .OrderByDescending(x => x.Value)
            .Take(6)
            .Select(x => x.Key)
            .ToList();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Sku, x.Name })
            .ToListAsync(cancellationToken);

        return productIds
            .Select(id => products.SingleOrDefault(x => x.Id == id) is { } product
                ? new DashboardProductRankDto(id, product.Sku, product.Name, totals[id])
                : null)
            .Where(x => x is not null)
            .Cast<DashboardProductRankDto>()
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardRecentOperationDto>> BuildRecentOperationsAsync(CancellationToken cancellationToken)
    {
        var recent = new List<DashboardRecentOperationDto>();
        var orders = await dbContext.SalesOrders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => new { x.Id, x.OrderNumber, x.CustomerName, Quantity = x.Items.Sum(i => i.Quantity), x.Status, Date = x.CreatedAt })
            .ToListAsync(cancellationToken);
        recent.AddRange(orders.Select(x =>
            new DashboardRecentOperationDto(x.Id, "Sipariş", x.OrderNumber, x.CustomerName, x.Quantity, x.Status.ToString(), x.Date)));

        var purchaseRequests = await dbContext.PurchaseRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => new { x.Id, x.RequestNumber, x.SupplierName, Quantity = x.Items.Sum(i => i.Quantity), x.Status, Date = x.CreatedAt })
            .ToListAsync(cancellationToken);
        recent.AddRange(purchaseRequests.Select(x =>
            new DashboardRecentOperationDto(x.Id, "Alım", x.RequestNumber, x.SupplierName, x.Quantity, x.Status.ToString(), x.Date)));

        var shipments = await dbContext.Shipments
            .AsNoTracking()
            .OrderByDescending(x => x.ShippedAt)
            .Take(8)
            .Select(x => new { x.Id, x.ShipmentNumber, x.RecipientName, Quantity = x.Items.Sum(i => i.Quantity), x.Status, Date = x.ShippedAt })
            .ToListAsync(cancellationToken);
        recent.AddRange(shipments.Select(x =>
            new DashboardRecentOperationDto(x.Id, "Sevkiyat", x.ShipmentNumber, x.RecipientName, x.Quantity, x.Status.ToString(), x.Date)));

        var returns = await dbContext.ReturnRequests
            .AsNoTracking()
            .OrderByDescending(x => x.ReceivedAt)
            .Take(8)
            .Select(x => new { x.Id, x.ReturnNumber, x.CustomerName, Quantity = x.Items.Sum(i => i.Quantity), x.Status, Date = x.ReceivedAt })
            .ToListAsync(cancellationToken);
        recent.AddRange(returns.Select(x =>
            new DashboardRecentOperationDto(x.Id, "İade", x.ReturnNumber, x.CustomerName, x.Quantity, x.Status.ToString(), x.Date)));

        return recent
            .OrderByDescending(x => x.Date)
            .Take(8)
            .ToList();
    }

    private static int CountBy<TItem, TValue>(
        IReadOnlyList<TItem> stats,
        TValue value,
        Func<TItem, TValue> valueSelector,
        Func<TItem, int> countSelector)
        where TValue : struct, Enum
    {
        var item = stats.FirstOrDefault(x => EqualityComparer<TValue>.Default.Equals(valueSelector(x), value));
        return item is null ? 0 : countSelector(item);
    }

    private static IReadOnlyList<(DateTimeOffset Start, string Key, string Label)> RecentDays(int dayCount)
    {
        var today = DateTimeOffset.UtcNow.Date;
        return Enumerable.Range(0, dayCount)
            .Select(index =>
            {
                var date = new DateTimeOffset(today.AddDays(-(dayCount - 1 - index)), TimeSpan.Zero);
                return (date, DateKey(date), date.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture));
            })
            .ToList();
    }

    private static string DateKey(DateTimeOffset date)
    {
        return date.UtcDateTime.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddTotals(Dictionary<Guid, int> totals, IReadOnlyList<ProductQuantity> quantities)
    {
        foreach (var item in quantities)
        {
            totals[item.ProductId] = totals.GetValueOrDefault(item.ProductId) + item.Quantity;
        }
    }

    private sealed class StockFlowAccumulator
    {
        public int Inbound { get; set; }
        public int Outbound { get; set; }
    }

    private sealed record ProductQuantity(Guid ProductId, int Quantity);
}
