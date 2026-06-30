using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Warehouses;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class WarehouseService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null,
    IMetricsRecorder? metricsRecorder = null) : IWarehouseService
{
    public async Task<PagedResult<WarehouseDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        await EnsureWarehouseBaselineAsync(cancellationToken);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.Warehouses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                (x.Address != null && x.Address.ToLower().Contains(term)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var warehouses = await query
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var warehouseIds = warehouses.Select(x => x.Id).ToList();
        var stockStats = await dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .GroupBy(x => x.WarehouseId)
            .Select(x => new { WarehouseId = x.Key, ProductCount = x.Count(s => s.Quantity > 0), TotalQuantity = x.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.WarehouseId, cancellationToken);

        var items = warehouses.Select(x =>
        {
            var hasStats = stockStats.TryGetValue(x.Id, out var stats);
            return ToDto(x, hasStats ? stats!.ProductCount : 0, hasStats ? stats!.TotalQuantity : 0);
        }).ToList();

        return new PagedResult<WarehouseDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, null, cancellationToken);

        var makeDefault = request.IsDefault || !await dbContext.Warehouses.AnyAsync(cancellationToken);
        if (makeDefault)
        {
            await ClearDefaultAsync(cancellationToken);
        }

        var warehouse = new Warehouse
        {
            TenantId = currentTenant.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            IsDefault = makeDefault
        };

        dbContext.Warehouses.Add(warehouse);
        auditWriter.AddSnapshot("warehouse.created", nameof(Warehouse), warehouse.Id, null, WarehouseSnapshot(warehouse));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(warehouse, 0, 0);
    }

    public async Task<WarehouseDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var warehouse = await FindWarehouseAsync(id, cancellationToken);
        var oldValue = WarehouseSnapshot(warehouse);
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, warehouse.Id, cancellationToken);

        if (!request.IsActive && warehouse.IsDefault)
        {
            throw new AppProblemException(400, "default_warehouse_required", "Default warehouse cannot be deactivated.");
        }

        var newName = request.Name.Trim();
        var nameChanged = !string.Equals(warehouse.Name, newName, StringComparison.Ordinal);

        if (request.IsDefault)
        {
            await ClearDefaultAsync(cancellationToken);
        }
        else if (warehouse.IsDefault)
        {
            warehouse.IsDefault = true;
        }

        warehouse.Code = code;
        warehouse.Name = newName;
        warehouse.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        warehouse.IsDefault = request.IsDefault || warehouse.IsDefault;
        warehouse.IsActive = request.IsActive;

        if (nameChanged)
        {
            await RefreshOperationSearchTextForWarehouseAsync(warehouse.Id, warehouse.Name, cancellationToken);
        }

        auditWriter.AddSnapshot("warehouse.updated", nameof(Warehouse), warehouse.Id, oldValue, WarehouseSnapshot(warehouse));
        await dbContext.SaveChangesAsync(cancellationToken);

        var stats = await WarehouseStatsAsync(warehouse.Id, cancellationToken);
        return ToDto(warehouse, stats.ProductCount, stats.TotalQuantity);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var warehouse = await FindWarehouseAsync(id, cancellationToken);
        if (warehouse.IsDefault)
        {
            throw new AppProblemException(400, "default_warehouse_required", "Default warehouse cannot be deactivated.");
        }

        var hasStock = await dbContext.WarehouseStocks.AnyAsync(x => x.WarehouseId == id && x.Quantity > 0, cancellationToken);
        if (hasStock)
        {
            throw new AppProblemException(400, "warehouse_has_stock", "Warehouse with stock cannot be deactivated.");
        }

        var oldValue = WarehouseSnapshot(warehouse);
        warehouse.IsActive = false;
        auditWriter.AddSnapshot("warehouse.deactivated", nameof(Warehouse), warehouse.Id, oldValue, WarehouseSnapshot(warehouse));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<WarehouseStockDto>> ListStockAsync(Guid? warehouseId, Guid? productId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        await EnsureWarehouseBaselineAsync(cancellationToken);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.WarehouseStocks
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Product)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        query = query.Where(x => x.Product.IsActive);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Warehouse.Name)
            .ThenBy(x => x.Product.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new WarehouseStockDto(
                x.WarehouseId,
                x.Warehouse.Code,
                x.Warehouse.Name,
                x.ProductId,
                x.Product.Sku,
                x.Product.Name,
                x.Quantity,
                x.Product.CriticalStockLevel,
                x.Quantity <= x.Product.CriticalStockLevel))
            .ToListAsync(cancellationToken);

        return new PagedResult<WarehouseStockDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<StockTransferDto> TransferAsync(StockTransferRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        const string operationName = "warehouse.transfer";
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Warehouse transfer" : request.Reason.Trim();
        var requestFingerprint = new
        {
            request.ProductId,
            request.FromWarehouseId,
            request.ToWarehouseId,
            request.Quantity,
            Reason = reason
        };
        return await TransactionRunner.RunAsync(async ct =>
        {
            var existing = await Idempotency.TryReserveAsync(operationName, requestFingerprint, ct);
            if (existing is not null)
            {
                return Idempotency.TryReadResponseSnapshot<StockTransferDto>(existing)
                    ?? await FindTransferDtoByIdempotencyRecordAsync(existing, ct);
            }

            var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive, ct);
            if (product is null)
            {
                throw new AppProblemException(404, "product_not_found", "Product was not found.");
            }

            var fromStock = await stockLedger.GetOrCreateStockAsync(product, request.FromWarehouseId, ct);
            var toStock = await stockLedger.GetOrCreateStockAsync(product, request.ToWarehouseId, ct);
            var transferStocks = new[] { fromStock, toStock }
                .OrderBy(x => x.WarehouseId)
                .ThenBy(x => x.ProductId)
                .ToArray();
            await stockLedger.LockForStockWriteAsync([product], transferStocks, ct);
            if (fromStock.WarehouseId == toStock.WarehouseId)
            {
                throw new AppProblemException(400, "same_warehouse_transfer", "Source and target warehouses must be different.");
            }

            if (fromStock.Quantity < request.Quantity)
            {
                throw new AppProblemException(400, "insufficient_warehouse_stock", "Source warehouse stock is insufficient.");
            }

            var transferGroupId = Guid.CreateVersion7();
            var fromPrevious = fromStock.Quantity;
            var toPrevious = toStock.Quantity;
            fromStock.Quantity -= request.Quantity;
            toStock.Quantity += request.Quantity;

            var outMovement = new StockMovement
            {
                TenantId = currentTenant.TenantId,
                ProductId = product.Id,
                WarehouseId = fromStock.WarehouseId,
                TransferGroupId = transferGroupId,
                Type = StockMovementType.TransferOut,
                Quantity = request.Quantity,
                PreviousQuantity = fromPrevious,
                NewQuantity = fromStock.Quantity,
                Reason = reason,
                PerformedByUserId = currentUser.UserId
            };
            var inMovement = new StockMovement
            {
                TenantId = currentTenant.TenantId,
                ProductId = product.Id,
                WarehouseId = toStock.WarehouseId,
                TransferGroupId = transferGroupId,
                Type = StockMovementType.TransferIn,
                Quantity = request.Quantity,
                PreviousQuantity = toPrevious,
                NewQuantity = toStock.Quantity,
                Reason = reason,
                PerformedByUserId = currentUser.UserId
            };

            dbContext.StockMovements.AddRange(outMovement, inMovement);
            auditWriter.AddSnapshot(
                "warehouse.transfer_created",
                nameof(StockMovement),
                transferGroupId,
                new { product.Id, product.Sku, FromWarehouseId = fromStock.WarehouseId, ToWarehouseId = toStock.WarehouseId, request.Quantity },
                null,
                new { OutMovementId = outMovement.Id, InMovementId = inMovement.Id });
            await dbContext.SaveChangesAsync(ct);

            var dto = new StockTransferDto(
                transferGroupId,
                product.Id,
                product.Sku,
                product.Name,
                fromStock.WarehouseId,
                fromStock.Warehouse.Name,
                toStock.WarehouseId,
                toStock.Warehouse.Name,
                request.Quantity,
                outMovement.CreatedAt);

            if (await Idempotency.CompleteAsync(operationName, requestFingerprint, nameof(StockMovement), transferGroupId.ToString(), dto, ct))
            {
                await dbContext.SaveChangesAsync(ct);
            }

            metricsRecorder?.RecordStockMovement(outMovement.Type, outMovement.Quantity, product.CurrentStock <= product.CriticalStockLevel);
            metricsRecorder?.RecordStockMovement(inMovement.Type, inMovement.Quantity, product.CurrentStock <= product.CriticalStockLevel);

            return dto;
        }, cancellationToken);
    }

    private async Task<StockTransferDto> FindTransferDtoByIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(record.ResourceId, out var transferGroupId))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        var movements = await dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.TransferGroupId == transferGroupId)
            .ToListAsync(cancellationToken);
        var outMovement = movements.SingleOrDefault(x => x.Type == StockMovementType.TransferOut);
        var inMovement = movements.SingleOrDefault(x => x.Type == StockMovementType.TransferIn);

        if (outMovement is null || inMovement is null || outMovement.ProductId != inMovement.ProductId)
        {
            throw new AppProblemException(409, "idempotency_resource_missing", "Idempotency record points to a missing resource.");
        }

        return new StockTransferDto(
            transferGroupId,
            outMovement.ProductId,
            outMovement.Product.Sku,
            outMovement.Product.Name,
            outMovement.WarehouseId!.Value,
            outMovement.Warehouse!.Name,
            inMovement.WarehouseId!.Value,
            inMovement.Warehouse!.Name,
            outMovement.Quantity,
            outMovement.CreatedAt);
    }

    private async Task<(int ProductCount, int TotalQuantity)> WarehouseStatsAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var stocks = await dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        return (stocks.Count(x => x.Quantity > 0), stocks.Sum(x => x.Quantity));
    }

    private async Task EnsureWarehouseBaselineAsync(CancellationToken cancellationToken)
    {
        await stockLedger.GetDefaultWarehouseAsync(cancellationToken);
        var products = await dbContext.Products
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var product in products)
        {
            await stockLedger.SeedProductIfMissingAsync(product, cancellationToken);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Warehouse> FindWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return warehouse ?? throw new AppProblemException(404, "warehouse_not_found", "Warehouse was not found.");
    }

    private async Task ClearDefaultAsync(CancellationToken cancellationToken)
    {
        var defaults = await dbContext.Warehouses.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var warehouse in defaults)
        {
            warehouse.IsDefault = false;
        }
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? currentWarehouseId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Warehouses.AnyAsync(
            x => x.Code == code && (!currentWarehouseId.HasValue || x.Id != currentWarehouseId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "warehouse_code_exists", "A warehouse with this code already exists.");
        }
    }

    private async Task RefreshOperationSearchTextForWarehouseAsync(Guid warehouseId, string warehouseName, CancellationToken cancellationToken)
    {
        var orders = await dbContext.SalesOrders
            .Where(x => x.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        foreach (var order in orders)
        {
            order.SearchText = OperationSearchText.Build(order.OrderNumber, order.CustomerName, warehouseName);
        }

        var purchases = await dbContext.PurchaseRequests
            .Where(x => x.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        foreach (var purchase in purchases)
        {
            purchase.SearchText = OperationSearchText.Build(purchase.RequestNumber, purchase.SupplierName, warehouseName);
        }

        var shipments = await dbContext.Shipments
            .Include(x => x.SalesOrder)
            .Where(x => x.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        foreach (var shipment in shipments)
        {
            shipment.SearchText = OperationSearchText.Build(
                shipment.ShipmentNumber,
                shipment.RecipientName,
                shipment.TrackingNumber,
                warehouseName,
                shipment.SalesOrder?.OrderNumber);
        }

        var returns = await dbContext.ReturnRequests
            .Include(x => x.SalesOrder)
            .Where(x => x.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        foreach (var returnRequest in returns)
        {
            returnRequest.SearchText = OperationSearchText.Build(
                returnRequest.ReturnNumber,
                returnRequest.CustomerName,
                returnRequest.Reason,
                warehouseName,
                returnRequest.SalesOrder?.OrderNumber);
        }
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    private static WarehouseDto ToDto(Warehouse warehouse, int productCount, int totalQuantity)
    {
        return new WarehouseDto(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.Address,
            warehouse.IsDefault,
            warehouse.IsActive,
            productCount,
            totalQuantity);
    }

    private static object WarehouseSnapshot(Warehouse warehouse)
    {
        return new
        {
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.Address,
            warehouse.IsDefault,
            warehouse.IsActive
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
