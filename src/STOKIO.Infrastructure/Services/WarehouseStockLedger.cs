using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class WarehouseStockLedger(StokioDbContext dbContext, ICurrentTenant currentTenant)
{
    private bool UsesPostgresRowLocks =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<Warehouse> ResolveWarehouseAsync(Guid? warehouseId, CancellationToken cancellationToken)
    {
        EnsureTenant();

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            var localWarehouse = dbContext.Warehouses.Local.SingleOrDefault(
                x => x.Id == warehouseId.Value && x.TenantId == currentTenant.TenantId && x.IsActive);
            if (localWarehouse is not null)
            {
                return localWarehouse;
            }

            var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(
                x => x.Id == warehouseId.Value && x.IsActive,
                cancellationToken);

            return warehouse ?? throw new AppProblemException(404, "warehouse_not_found", "Warehouse was not found.");
        }

        return await GetDefaultWarehouseAsync(cancellationToken);
    }

    public async Task<Warehouse> GetDefaultWarehouseAsync(CancellationToken cancellationToken)
    {
        EnsureTenant();
        var localWarehouse = dbContext.Warehouses.Local
            .Where(x => x.TenantId == currentTenant.TenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .FirstOrDefault();
        if (localWarehouse is not null)
        {
            return localWarehouse;
        }

        var warehouse = await dbContext.Warehouses
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

        if (warehouse is not null)
        {
            return warehouse;
        }

        warehouse = new Warehouse
        {
            TenantId = currentTenant.TenantId,
            Code = "MAIN",
            Name = "Ana Depo",
            IsDefault = true
        };
        dbContext.Warehouses.Add(warehouse);
        return warehouse;
    }

    public async Task<WarehouseStock> GetOrCreateStockAsync(Product product, Guid? warehouseId, CancellationToken cancellationToken)
    {
        await SeedProductIfMissingAsync(product, cancellationToken);
        var warehouse = await ResolveWarehouseAsync(warehouseId, cancellationToken);

        var localStock = dbContext.WarehouseStocks.Local.FirstOrDefault(
            x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id);
        if (localStock is not null)
        {
            return localStock;
        }

        var stock = await dbContext.WarehouseStocks
            .Include(x => x.Warehouse)
            .SingleOrDefaultAsync(
                x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id,
                cancellationToken);

        if (stock is not null)
        {
            return stock;
        }

        stock = new WarehouseStock
        {
            TenantId = currentTenant.TenantId,
            ProductId = product.Id,
            Product = product,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Quantity = 0
        };
        dbContext.WarehouseStocks.Add(stock);
        return stock;
    }

    public async Task LockForStockWriteAsync(
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<WarehouseStock> warehouseStocks,
        CancellationToken cancellationToken)
    {
        EnsureTenant();

        if (!UsesPostgresRowLocks)
        {
            return;
        }

        if (products.Count == 0 && warehouseStocks.Count == 0)
        {
            return;
        }

        var hasNewLockTargets = dbContext.ChangeTracker.Entries()
            .Any(x => x.State == EntityState.Added && x.Entity is Warehouse or WarehouseStock);
        if (hasNewLockTargets)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var productIds = products
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        if (productIds.Length > 0)
        {
            var lockedProducts = await dbContext.Products
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Products"
                    WHERE "TenantId" = {currentTenant.TenantId}
                      AND "Id" = ANY({productIds})
                    ORDER BY "Id"
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);

            if (lockedProducts.Count != productIds.Length)
            {
                throw new AppProblemException(409, "stock_lock_product_missing", "One or more stock product rows could not be locked.");
            }

            foreach (var product in products.OrderBy(x => x.Id))
            {
                await dbContext.Entry(product).ReloadAsync(cancellationToken);
            }
        }

        var stockIds = warehouseStocks
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        if (stockIds.Length == 0)
        {
            return;
        }

        var lockedStocks = await dbContext.WarehouseStocks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "WarehouseStocks"
                WHERE "TenantId" = {currentTenant.TenantId}
                  AND "Id" = ANY({stockIds})
                ORDER BY "WarehouseId", "ProductId"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        if (lockedStocks.Count != stockIds.Length)
        {
            throw new AppProblemException(409, "stock_lock_balance_missing", "One or more warehouse stock rows could not be locked.");
        }

        foreach (var stock in warehouseStocks.OrderBy(x => x.WarehouseId).ThenBy(x => x.ProductId))
        {
            await dbContext.Entry(stock).ReloadAsync(cancellationToken);
        }
    }

    public async Task SeedProductIfMissingAsync(Product product, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var hasLocalStock = dbContext.WarehouseStocks.Local.Any(x => x.ProductId == product.Id);
        if (hasLocalStock)
        {
            return;
        }

        var hasStock = await dbContext.WarehouseStocks.AnyAsync(x => x.ProductId == product.Id, cancellationToken);
        if (hasStock)
        {
            return;
        }

        var warehouse = await GetDefaultWarehouseAsync(cancellationToken);
        dbContext.WarehouseStocks.Add(new WarehouseStock
        {
            TenantId = currentTenant.TenantId,
            ProductId = product.Id,
            Product = product,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Quantity = product.CurrentStock
        });
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }
}
