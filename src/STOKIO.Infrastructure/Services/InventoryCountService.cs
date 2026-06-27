using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Counts;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class InventoryCountService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null) : IInventoryCountService
{
    public async Task<InventoryCountDto> CreateAsync(CreateInventoryCountRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var products = await dbContext.Products
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, cancellationToken);

        var count = new InventoryCount
        {
            TenantId = currentTenant.TenantId,
            Name = request.Name.Trim(),
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Status = InventoryCountStatus.Open,
            StartedAt = clock.UtcNow,
            StartedByUserId = currentUser.UserId
        };

        foreach (var product in products)
        {
            var warehouseStock = await stockLedger.GetOrCreateStockAsync(product, warehouse.Id, cancellationToken);
            count.Items.Add(new InventoryCountItem
            {
                TenantId = currentTenant.TenantId,
                ProductId = product.Id,
                ExpectedQuantity = warehouseStock.Quantity,
                CountedQuantity = 0
            });
        }

        dbContext.InventoryCounts.Add(count);
        auditWriter.AddSnapshot("inventory_count.created", nameof(InventoryCount), count.Id, null, CountSnapshot(count));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(count);
    }

    public async Task<InventoryCountDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var count = await FindCountAsync(id, includeProducts: false, cancellationToken);
        return ToDto(count);
    }

    public async Task<InventoryCountItemDto> ScanAsync(Guid countId, ScanCountItemRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var count = await FindCountAsync(countId, includeProducts: true, cancellationToken);
        EnsureOpen(count);

        var barcode = request.Barcode.Trim();
        var product = await dbContext.ProductBarcodes
            .Include(x => x.Product)
            .Where(x => x.Barcode == barcode && x.Product.IsActive)
            .Select(x => x.Product)
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            throw new AppProblemException(404, "barcode_not_found", "Barcode was not assigned to an active product.");
        }

        var item = count.Items.SingleOrDefault(x => x.ProductId == product.Id);
        if (item is null)
        {
            var warehouseStock = await stockLedger.GetOrCreateStockAsync(product, count.WarehouseId, cancellationToken);
            item = new InventoryCountItem
            {
                TenantId = currentTenant.TenantId,
                InventoryCountId = count.Id,
                ProductId = product.Id,
                Product = product,
                ExpectedQuantity = warehouseStock.Quantity,
                CountedQuantity = 0
            };
            count.Items.Add(item);
        }

        item.CountedQuantity += request.Quantity;
        auditWriter.AddSnapshot(
            "inventory_count.item_scanned",
            nameof(InventoryCountItem),
            item.Id,
            null,
            ItemSnapshot(item),
            new { CountId = count.Id, product.Sku, request.Quantity });
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItemDto(item);
    }

    public async Task<InventoryCountDto> CloseAsync(Guid countId, CloseInventoryCountRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        const string operationName = "inventory_count.close";
        var requestFingerprint = new { CountId = countId, request.ApplyDifferences };
        var existing = await Idempotency.FindExistingAsync(operationName, requestFingerprint, cancellationToken);
        if (existing is not null)
        {
            return await FindDtoByIdempotencyRecordAsync(existing, cancellationToken);
        }

        return await TransactionRunner.RunAsync(async ct =>
        {
            var count = await FindCountAsync(countId, includeProducts: true, ct);
            EnsureOpen(count);

            if (request.ApplyDifferences)
            {
                foreach (var item in count.Items.Where(x => x.CountedQuantity != x.ExpectedQuantity))
                {
                    var warehouseStock = await stockLedger.GetOrCreateStockAsync(item.Product, count.WarehouseId, ct);
                    var previous = warehouseStock.Quantity;
                    var next = item.CountedQuantity;
                    warehouseStock.Quantity = next;
                    item.Product.CurrentStock += next - previous;
                    dbContext.StockMovements.Add(new StockMovement
                    {
                        TenantId = currentTenant.TenantId,
                        ProductId = item.ProductId,
                        WarehouseId = warehouseStock.WarehouseId,
                        Type = StockMovementType.CountCorrection,
                        Quantity = item.CountedQuantity,
                        PreviousQuantity = previous,
                        NewQuantity = item.CountedQuantity,
                        Reason = $"Inventory count correction: {count.Name}",
                        PerformedByUserId = currentUser.UserId
                    });
                }
            }

            count.Status = InventoryCountStatus.Closed;
            count.ClosedAt = clock.UtcNow;
            auditWriter.AddSnapshot("inventory_count.closed", nameof(InventoryCount), count.Id, null, CountSnapshot(count), new { request.ApplyDifferences });
            Idempotency.AddCompleted(operationName, requestFingerprint, nameof(InventoryCount), count.Id.ToString());
            await dbContext.SaveChangesAsync(ct);
            return ToDto(count);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryCountDifferenceDto>> GetDifferencesAsync(Guid countId, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var count = await FindCountAsync(countId, includeProducts: true, cancellationToken);
        return count.Items
            .Where(x => x.CountedQuantity != x.ExpectedQuantity)
            .OrderBy(x => x.Product.Name)
            .Select(x => new InventoryCountDifferenceDto(
                x.ProductId,
                x.Product.Sku,
                x.Product.Name,
                x.ExpectedQuantity,
                x.CountedQuantity,
                x.CountedQuantity - x.ExpectedQuantity))
            .ToList();
    }

    private async Task<InventoryCount> FindCountAsync(Guid countId, bool includeProducts, CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryCounts
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .AsQueryable();

        if (includeProducts)
        {
            query = query.Include(x => x.Items).ThenInclude(x => x.Product);
        }

        var count = await query.SingleOrDefaultAsync(x => x.Id == countId, cancellationToken);
        return count ?? throw new AppProblemException(404, "inventory_count_not_found", "Inventory count was not found.");
    }

    private async Task<InventoryCountDto> FindDtoByIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(record.ResourceId, out var id))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        return ToDto(await FindCountAsync(id, includeProducts: false, cancellationToken));
    }

    private static InventoryCountDto ToDto(InventoryCount count)
    {
        return new InventoryCountDto(
            count.Id,
            count.Name,
            count.WarehouseId,
            count.Warehouse?.Name,
            count.Status,
            count.StartedAt,
            count.ClosedAt,
            count.Items.Count,
            count.Items.Count(x => x.CountedQuantity != x.ExpectedQuantity));
    }

    private static object CountSnapshot(InventoryCount count)
    {
        return new
        {
            count.Id,
            count.Name,
            count.WarehouseId,
            WarehouseName = count.Warehouse?.Name,
            count.Status,
            count.StartedAt,
            count.ClosedAt,
            ItemCount = count.Items.Count,
            DifferenceCount = count.Items.Count(x => x.CountedQuantity != x.ExpectedQuantity)
        };
    }

    private static object ItemSnapshot(InventoryCountItem item)
    {
        return new
        {
            item.Id,
            item.InventoryCountId,
            item.ProductId,
            item.ExpectedQuantity,
            item.CountedQuantity,
            Difference = item.CountedQuantity - item.ExpectedQuantity
        };
    }

    private static InventoryCountItemDto ToItemDto(InventoryCountItem item)
    {
        return new InventoryCountItemDto(
            item.ProductId,
            item.Product.Sku,
            item.Product.Name,
            item.ExpectedQuantity,
            item.CountedQuantity,
            item.CountedQuantity - item.ExpectedQuantity);
    }

    private static void EnsureOpen(InventoryCount count)
    {
        if (count.Status != InventoryCountStatus.Open)
        {
            throw new AppProblemException(400, "inventory_count_not_open", "Only open inventory counts can be changed.");
        }
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }

    private IdempotencyService Idempotency => idempotencyService ?? new IdempotencyService(dbContext, currentTenant);

    private DbTransactionRunner TransactionRunner => transactionRunner ?? new DbTransactionRunner(dbContext);
}
