using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class StockService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null) : IStockService
{
    public async Task<StockMovementDto> CreateMovementAsync(CreateStockMovementRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        const string operationName = "stock.movement";
        var requestFingerprint = new
        {
            request.ProductId,
            request.WarehouseId,
            request.Type,
            request.Quantity,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        return await TransactionRunner.RunAsync(async ct =>
        {
            var existing = await Idempotency.TryReserveAsync(operationName, requestFingerprint, ct);
            if (existing is not null)
            {
                return Idempotency.TryReadResponseSnapshot<StockMovementDto>(existing)
                    ?? await FindMovementDtoAsync(existing.ResourceId, ct);
            }

            var product = await dbContext.Products.SingleOrDefaultAsync(
                x => x.Id == request.ProductId && x.IsActive,
                ct);

            if (product is null)
            {
                throw new AppProblemException(404, "product_not_found", "Product was not found.");
            }

            var warehouseStock = await stockLedger.GetOrCreateStockAsync(product, request.WarehouseId, ct);
            var previous = warehouseStock.Quantity;
            var next = request.Type switch
            {
                StockMovementType.In => previous + request.Quantity,
                StockMovementType.Out => previous - request.Quantity,
                StockMovementType.Adjustment => request.Quantity,
                StockMovementType.CountCorrection => request.Quantity,
                _ => throw new AppProblemException(400, "invalid_movement_type", "Invalid stock movement type.")
            };

            if (next < 0)
            {
                throw new AppProblemException(400, "insufficient_stock", "Stock cannot be reduced below zero.");
            }

            var stockDelta = next - previous;
            if (product.CurrentStock + stockDelta < 0)
            {
                throw new AppProblemException(400, "insufficient_stock", "Stock cannot be reduced below zero.");
            }

            var oldValue = new { product.Id, product.Sku, product.Name, product.CurrentStock, WarehouseId = warehouseStock.WarehouseId, WarehouseStock = previous };
            warehouseStock.Quantity = next;
            product.CurrentStock += stockDelta;
            var movement = new StockMovement
            {
                TenantId = currentTenant.TenantId,
                ProductId = product.Id,
                WarehouseId = warehouseStock.WarehouseId,
                Type = request.Type,
                Quantity = request.Quantity,
                PreviousQuantity = previous,
                NewQuantity = next,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                PerformedByUserId = currentUser.UserId
            };

            dbContext.StockMovements.Add(movement);
            auditWriter.AddSnapshot(
                "stock.movement_created",
                nameof(StockMovement),
                movement.Id,
                oldValue,
                new { product.Id, product.Sku, product.Name, product.CurrentStock, WarehouseId = warehouseStock.WarehouseId, WarehouseStock = next },
                MovementSnapshot(movement));
            await dbContext.SaveChangesAsync(ct);

            var dto = new StockMovementDto(
                movement.Id,
                product.Id,
                product.Name,
                product.Sku,
                warehouseStock.WarehouseId,
                warehouseStock.Warehouse.Name,
                movement.Type,
                movement.Quantity,
                movement.PreviousQuantity,
                movement.NewQuantity,
                movement.Reason,
                movement.CreatedAt);

            if (await Idempotency.CompleteAsync(operationName, requestFingerprint, nameof(StockMovement), movement.Id.ToString(), dto, ct))
            {
                await dbContext.SaveChangesAsync(ct);
            }

            return dto;
        }, cancellationToken);
    }

    public async Task<PagedResult<StockMovementDto>> ListMovementsAsync(
        Guid? productId,
        Guid? warehouseId,
        StockMovementType? type,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var movements = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(
            movements.Select(ToDto).ToList(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<CriticalStockDto>> ListCriticalStockAsync(CancellationToken cancellationToken)
    {
        EnsureTenant();
        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.CurrentStock <= x.CriticalStockLevel)
            .OrderBy(x => x.CurrentStock)
            .ThenBy(x => x.Name)
            .Select(x => new CriticalStockDto(x.Id, x.Sku, x.Name, x.CurrentStock, x.CriticalStockLevel))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockConsistencyDto>> CheckConsistencyAsync(CancellationToken cancellationToken)
    {
        EnsureTenant();
        var products = await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var productIds = products.Select(x => x.Id).ToList();
        var movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var warehouseStocks = await dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync(cancellationToken);

        var movementLookup = movements.ToLookup(x => new { x.ProductId, x.WarehouseId });
        var stockLookup = warehouseStocks.ToLookup(x => x.ProductId);
        return products.Select(product =>
        {
            var issues = new List<string>();
            var ledgerStockByWarehouse = new Dictionary<Guid, int>();

            foreach (var warehouseGroup in movements.Where(x => x.ProductId == product.Id).GroupBy(x => x.WarehouseId))
            {
                var warehouseLedgerStock = 0;
                foreach (var movement in movementLookup[new { ProductId = product.Id, WarehouseId = warehouseGroup.Key }])
                {
                    if (movement.PreviousQuantity != warehouseLedgerStock)
                    {
                        issues.Add($"Movement {movement.Id} previous quantity is {movement.PreviousQuantity}, expected {warehouseLedgerStock}.");
                    }

                    var expectedNewQuantity = movement.Type switch
                    {
                        StockMovementType.In or StockMovementType.TransferIn => warehouseLedgerStock + movement.Quantity,
                        StockMovementType.Out or StockMovementType.TransferOut => warehouseLedgerStock - movement.Quantity,
                        StockMovementType.Adjustment => movement.Quantity,
                        StockMovementType.CountCorrection => movement.Quantity,
                        _ => warehouseLedgerStock
                    };

                    if (expectedNewQuantity < 0)
                    {
                        issues.Add($"Movement {movement.Id} would make stock negative.");
                    }

                    if (movement.NewQuantity != expectedNewQuantity)
                    {
                        issues.Add($"Movement {movement.Id} new quantity is {movement.NewQuantity}, expected {expectedNewQuantity}.");
                    }

                    warehouseLedgerStock = expectedNewQuantity;
                }

                ledgerStockByWarehouse[warehouseGroup.Key ?? Guid.Empty] = warehouseLedgerStock;
            }

            foreach (var warehouseStock in stockLookup[product.Id])
            {
                var key = warehouseStock.WarehouseId;
                var ledger = ledgerStockByWarehouse.GetValueOrDefault(key);
                if (warehouseStock.Quantity != ledger)
                {
                    issues.Add($"Warehouse stock {warehouseStock.WarehouseId} is {warehouseStock.Quantity}, ledger stock is {ledger}.");
                }
            }

            var ledgerStock = ledgerStockByWarehouse.Values.Sum();
            var storedWarehouseTotal = stockLookup[product.Id].Sum(x => x.Quantity);
            if (product.CurrentStock != storedWarehouseTotal)
            {
                issues.Add($"Product current stock is {product.CurrentStock}, warehouse stock total is {storedWarehouseTotal}.");
            }

            if (product.CurrentStock != ledgerStock)
            {
                issues.Add($"Product current stock is {product.CurrentStock}, ledger stock is {ledgerStock}.");
            }

            return new StockConsistencyDto(
                product.Id,
                product.Sku,
                product.Name,
                product.CurrentStock,
                ledgerStock,
                issues.Count == 0,
                issues);
        }).ToList();
    }

    private static StockMovementDto ToDto(StockMovement movement)
    {
        return new StockMovementDto(
            movement.Id,
            movement.ProductId,
            movement.Product.Name,
            movement.Product.Sku,
            movement.WarehouseId,
            movement.Warehouse?.Name,
            movement.Type,
            movement.Quantity,
            movement.PreviousQuantity,
            movement.NewQuantity,
            movement.Reason,
            movement.CreatedAt);
    }

    private async Task<StockMovementDto> FindMovementDtoAsync(string resourceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(resourceId, out var movementId))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        var movement = await dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .SingleOrDefaultAsync(x => x.Id == movementId, cancellationToken);

        return movement is null
            ? throw new AppProblemException(409, "idempotency_resource_missing", "Idempotency record points to a missing resource.")
            : ToDto(movement);
    }

    private static object MovementSnapshot(StockMovement movement)
    {
        return new
        {
            movement.Id,
            movement.ProductId,
            movement.WarehouseId,
            movement.TransferGroupId,
            movement.Type,
            movement.Quantity,
            movement.PreviousQuantity,
            movement.NewQuantity,
            movement.Reason
        };
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
