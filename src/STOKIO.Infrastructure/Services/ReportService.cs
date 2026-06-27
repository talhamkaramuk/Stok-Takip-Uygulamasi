using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Reports;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class ReportService(StokioDbContext dbContext, ICurrentTenant currentTenant) : IReportService
{
    public async Task<IReadOnlyList<CurrentStockReportRow>> CurrentStockAsync(CancellationToken cancellationToken)
    {
        EnsureTenant();
        return await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CurrentStockReportRow(
                x.Id,
                x.Sku,
                x.Name,
                x.Category == null ? null : x.Category.Name,
                x.CurrentStock,
                x.CriticalStockLevel,
                x.CurrentStock <= x.CriticalStockLevel))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovementReportRow>> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(1_000)
            .Select(x => new MovementReportRow(
                x.Id,
                x.ProductId,
                x.Product.Sku,
                x.Product.Name,
                x.Warehouse == null ? null : x.Warehouse.Name,
                x.Type,
                x.Quantity,
                x.PreviousQuantity,
                x.NewQuantity,
                x.Reason,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CountDifferenceReportRow>> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken)
    {
        EnsureTenant();
        return await dbContext.InventoryCountItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.InventoryCountId == countId && x.CountedQuantity != x.ExpectedQuantity)
            .OrderBy(x => x.Product.Name)
            .Select(x => new CountDifferenceReportRow(
                x.ProductId,
                x.Product.Sku,
                x.Product.Name,
                x.ExpectedQuantity,
                x.CountedQuantity,
                x.CountedQuantity - x.ExpectedQuantity))
            .ToListAsync(cancellationToken);
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }
}
