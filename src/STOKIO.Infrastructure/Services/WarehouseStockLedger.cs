using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class WarehouseStockLedger(StokioDbContext dbContext, ICurrentTenant currentTenant)
{
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
