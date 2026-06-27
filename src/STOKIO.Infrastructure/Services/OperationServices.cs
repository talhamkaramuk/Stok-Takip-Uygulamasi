using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class SalesOrderService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter) : ISalesOrderService
{
    public async Task<PagedResult<SalesOrderDto>> ListAsync(SalesOrderStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesOrderDto>(orders.Select(OperationMapper.ToDto).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, cancellationToken);
        var customer = await OperationParties.ResolveCustomerAsync(dbContext, request.CustomerId, cancellationToken);
        var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, cancellationToken);
        var order = new SalesOrder
        {
            TenantId = currentTenant.TenantId,
            OrderNumber = OperationNumbers.Next("SO", clock.UtcNow),
            CustomerId = customer?.Id,
            Customer = customer,
            CustomerName = OperationParties.NameOrFallback(customer, request.CustomerName),
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Notes = OperationText.Optional(request.Notes),
            CreatedByUserId = currentUser.UserId
        };

        foreach (var item in items)
        {
            order.Items.Add(new SalesOrderItem
            {
                TenantId = currentTenant.TenantId,
                ProductId = item.Product.Id,
                Product = item.Product,
                Quantity = item.Quantity
            });
        }

        dbContext.SalesOrders.Add(order);
        auditWriter.AddSnapshot("sales_order.created", nameof(SalesOrder), order.Id, null, OperationMapper.OrderSnapshot(order));
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationMapper.ToDto(order);
    }
}

public sealed class PurchaseRequestService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null) : IPurchaseRequestService
{
    public async Task<PagedResult<PurchaseRequestDto>> ListAsync(PurchaseRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.PurchaseRequests
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseRequestDto>(requests.Select(OperationMapper.ToDto).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, cancellationToken);
        var supplier = await OperationParties.ResolveSupplierAsync(dbContext, request.SupplierId, cancellationToken);
        var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, cancellationToken);
        var purchaseRequest = new PurchaseRequest
        {
            TenantId = currentTenant.TenantId,
            RequestNumber = OperationNumbers.Next("PR", clock.UtcNow),
            SupplierId = supplier?.Id,
            Supplier = supplier,
            SupplierName = OperationParties.NameOrFallback(supplier, request.SupplierName),
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Notes = OperationText.Optional(request.Notes),
            RequestedByUserId = currentUser.UserId
        };

        foreach (var item in items)
        {
            purchaseRequest.Items.Add(new PurchaseRequestItem
            {
                TenantId = currentTenant.TenantId,
                ProductId = item.Product.Id,
                Product = item.Product,
                Quantity = item.Quantity
            });
        }

        dbContext.PurchaseRequests.Add(purchaseRequest);
        auditWriter.AddSnapshot("purchase_request.created", nameof(PurchaseRequest), purchaseRequest.Id, null, OperationMapper.PurchaseSnapshot(purchaseRequest));
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationMapper.ToDto(purchaseRequest);
    }

    public async Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var purchaseRequest = await FindAsync(id, cancellationToken);
        if (purchaseRequest.Status != PurchaseRequestStatus.PendingApproval)
        {
            throw new AppProblemException(400, "purchase_request_not_pending", "Only pending purchase requests can be approved.");
        }

        purchaseRequest.Status = PurchaseRequestStatus.Approved;
        purchaseRequest.ApprovedAt = clock.UtcNow;
        auditWriter.AddSnapshot("purchase_request.approved", nameof(PurchaseRequest), purchaseRequest.Id, null, OperationMapper.PurchaseSnapshot(purchaseRequest));
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationMapper.ToDto(purchaseRequest);
    }

    public async Task<PurchaseRequestDto> ReceiveAsync(Guid id, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        const string operationName = "purchase_request.receive";
        var requestFingerprint = new { PurchaseRequestId = id };
        var existing = await Idempotency.FindExistingAsync(operationName, requestFingerprint, cancellationToken);
        if (existing is not null)
        {
            return await FindDtoByIdempotencyRecordAsync(existing, cancellationToken);
        }

        return await TransactionRunner.RunAsync(async ct =>
        {
            var purchaseRequest = await FindAsync(id, ct);
            if (purchaseRequest.Status is PurchaseRequestStatus.Received or PurchaseRequestStatus.Cancelled)
            {
                throw new AppProblemException(400, "purchase_request_closed", "Closed purchase requests cannot be received.");
            }

            purchaseRequest.Status = PurchaseRequestStatus.Received;
            purchaseRequest.ApprovedAt ??= clock.UtcNow;
            purchaseRequest.ReceivedAt = clock.UtcNow;
            foreach (var item in purchaseRequest.Items)
            {
                await OperationStock.ApplyAsync(
                    dbContext,
                    currentTenant,
                    currentUser,
                    stockLedger,
                    item.Product,
                    purchaseRequest.WarehouseId,
                    StockMovementType.In,
                    item.Quantity,
                    $"Purchase request received: {purchaseRequest.RequestNumber}",
                    ct);
            }

            auditWriter.AddSnapshot("purchase_request.received", nameof(PurchaseRequest), purchaseRequest.Id, null, OperationMapper.PurchaseSnapshot(purchaseRequest));
            Idempotency.AddCompleted(operationName, requestFingerprint, nameof(PurchaseRequest), purchaseRequest.Id.ToString());
            await dbContext.SaveChangesAsync(ct);
            return OperationMapper.ToDto(purchaseRequest);
        }, cancellationToken);
    }

    private async Task<PurchaseRequest> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchaseRequest = await dbContext.PurchaseRequests
            .Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return purchaseRequest ?? throw new AppProblemException(404, "purchase_request_not_found", "Purchase request was not found.");
    }

    private async Task<PurchaseRequestDto> FindDtoByIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(record.ResourceId, out var id))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        return OperationMapper.ToDto(await FindAsync(id, cancellationToken));
    }

    private IdempotencyService Idempotency => idempotencyService ?? new IdempotencyService(dbContext, currentTenant);

    private DbTransactionRunner TransactionRunner => transactionRunner ?? new DbTransactionRunner(dbContext);
}

public sealed class ShipmentService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null) : IShipmentService
{
    public async Task<PagedResult<ShipmentDto>> ListAsync(ShipmentStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.Shipments
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var shipments = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ShipmentDto>(shipments.Select(OperationMapper.ToDto).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<ShipmentDto> CreateAsync(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        const string operationName = "shipment.create";
        var requestFingerprint = OperationFingerprints.Shipment(request);
        var existing = await Idempotency.FindExistingAsync(operationName, requestFingerprint, cancellationToken);
        if (existing is not null)
        {
            return await FindDtoByIdempotencyRecordAsync(existing, cancellationToken);
        }

        return await TransactionRunner.RunAsync(async ct =>
        {
            var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, ct);
            var order = request.SalesOrderId.HasValue
                ? await dbContext.SalesOrders.SingleOrDefaultAsync(x => x.Id == request.SalesOrderId.Value, ct)
                : null;
            if (request.SalesOrderId.HasValue && order is null)
            {
                throw new AppProblemException(404, "sales_order_not_found", "Sales order was not found.");
            }

            var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, ct);
            var customerId = request.CustomerId ?? order?.CustomerId;
            var customer = await OperationParties.ResolveCustomerAsync(dbContext, customerId, ct);
            var shipment = new Shipment
            {
                TenantId = currentTenant.TenantId,
                ShipmentNumber = OperationNumbers.Next("SHP", clock.UtcNow),
                SalesOrderId = order?.Id,
                SalesOrder = order,
                CustomerId = customer?.Id,
                Customer = customer,
                RecipientName = OperationParties.NameOrFallback(customer, request.RecipientName),
                WarehouseId = warehouse.Id,
                Warehouse = warehouse,
                TrackingNumber = OperationText.Optional(request.TrackingNumber),
                Notes = OperationText.Optional(request.Notes),
                ShippedAt = clock.UtcNow
            };

            foreach (var item in items)
            {
                await OperationStock.ApplyAsync(
                    dbContext,
                    currentTenant,
                    currentUser,
                    stockLedger,
                    item.Product,
                    warehouse.Id,
                    StockMovementType.Out,
                    item.Quantity,
                    $"Shipment created: {shipment.ShipmentNumber}",
                    ct);

                shipment.Items.Add(new ShipmentItem
                {
                    TenantId = currentTenant.TenantId,
                    ProductId = item.Product.Id,
                    Product = item.Product,
                    Quantity = item.Quantity
                });
            }

            if (order is not null && order.Status != SalesOrderStatus.Cancelled)
            {
                order.Status = SalesOrderStatus.Shipped;
            }

            dbContext.Shipments.Add(shipment);
            auditWriter.AddSnapshot("shipment.created", nameof(Shipment), shipment.Id, null, OperationMapper.ShipmentSnapshot(shipment));
            Idempotency.AddCompleted(operationName, requestFingerprint, nameof(Shipment), shipment.Id.ToString());
            await dbContext.SaveChangesAsync(ct);
            return OperationMapper.ToDto(shipment);
        }, cancellationToken);
    }

    private async Task<ShipmentDto> FindDtoByIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(record.ResourceId, out var id))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return shipment is null
            ? throw new AppProblemException(409, "idempotency_resource_missing", "Idempotency record points to a missing resource.")
            : OperationMapper.ToDto(shipment);
    }

    private IdempotencyService Idempotency => idempotencyService ?? new IdempotencyService(dbContext, currentTenant);

    private DbTransactionRunner TransactionRunner => transactionRunner ?? new DbTransactionRunner(dbContext);
}

public sealed class ReturnRequestService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter,
    IdempotencyService? idempotencyService = null,
    DbTransactionRunner? transactionRunner = null) : IReturnRequestService
{
    public async Task<PagedResult<ReturnRequestDto>> ListAsync(ReturnRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.ReturnRequests
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var returns = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReturnRequestDto>(returns.Select(OperationMapper.ToDto).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<ReturnRequestDto> CreateAsync(CreateReturnRequestRequest request, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        const string operationName = "return_request.create";
        var requestFingerprint = OperationFingerprints.Return(request);
        var existing = await Idempotency.FindExistingAsync(operationName, requestFingerprint, cancellationToken);
        if (existing is not null)
        {
            return await FindDtoByIdempotencyRecordAsync(existing, cancellationToken);
        }

        return await TransactionRunner.RunAsync(async ct =>
        {
            var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, ct);
            var order = request.SalesOrderId.HasValue
                ? await dbContext.SalesOrders.SingleOrDefaultAsync(x => x.Id == request.SalesOrderId.Value, ct)
                : null;
            if (request.SalesOrderId.HasValue && order is null)
            {
                throw new AppProblemException(404, "sales_order_not_found", "Sales order was not found.");
            }

            var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, ct);
            var customerId = request.CustomerId ?? order?.CustomerId;
            var customer = await OperationParties.ResolveCustomerAsync(dbContext, customerId, ct);
            var returnRequest = new ReturnRequest
            {
                TenantId = currentTenant.TenantId,
                ReturnNumber = OperationNumbers.Next("RET", clock.UtcNow),
                SalesOrderId = order?.Id,
                SalesOrder = order,
                CustomerId = customer?.Id,
                Customer = customer,
                CustomerName = OperationParties.NameOrFallback(customer, request.CustomerName),
                WarehouseId = warehouse.Id,
                Warehouse = warehouse,
                Reason = request.Reason.Trim(),
                ReceivedAt = clock.UtcNow
            };

            foreach (var item in items)
            {
                await OperationStock.ApplyAsync(
                    dbContext,
                    currentTenant,
                    currentUser,
                    stockLedger,
                    item.Product,
                    warehouse.Id,
                    StockMovementType.In,
                    item.Quantity,
                    $"Return received: {returnRequest.ReturnNumber}",
                    ct);

                returnRequest.Items.Add(new ReturnRequestItem
                {
                    TenantId = currentTenant.TenantId,
                    ProductId = item.Product.Id,
                    Product = item.Product,
                    Quantity = item.Quantity
                });
            }

            dbContext.ReturnRequests.Add(returnRequest);
            auditWriter.AddSnapshot("return_request.created", nameof(ReturnRequest), returnRequest.Id, null, OperationMapper.ReturnSnapshot(returnRequest));
            Idempotency.AddCompleted(operationName, requestFingerprint, nameof(ReturnRequest), returnRequest.Id.ToString());
            await dbContext.SaveChangesAsync(ct);
            return OperationMapper.ToDto(returnRequest);
        }, cancellationToken);
    }

    private async Task<ReturnRequestDto> FindDtoByIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(record.ResourceId, out var id))
        {
            throw new AppProblemException(409, "idempotency_resource_invalid", "Idempotency record points to an invalid resource.");
        }

        var returnRequest = await dbContext.ReturnRequests
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return returnRequest is null
            ? throw new AppProblemException(409, "idempotency_resource_missing", "Idempotency record points to a missing resource.")
            : OperationMapper.ToDto(returnRequest);
    }

    private IdempotencyService Idempotency => idempotencyService ?? new IdempotencyService(dbContext, currentTenant);

    private DbTransactionRunner TransactionRunner => transactionRunner ?? new DbTransactionRunner(dbContext);
}

file static class OperationGuards
{
    public static void EnsureTenant(ICurrentTenant currentTenant)
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }
}

file static class OperationText
{
    public static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

file static class OperationParties
{
    public static async Task<Customer?> ResolveCustomerAsync(StokioDbContext dbContext, Guid? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return null;
        }

        var customer = await dbContext.Customers.SingleOrDefaultAsync(x => x.Id == customerId.Value && x.IsActive, cancellationToken);
        return customer ?? throw new AppProblemException(404, "customer_not_found", "Customer was not found.");
    }

    public static async Task<Supplier?> ResolveSupplierAsync(StokioDbContext dbContext, Guid? supplierId, CancellationToken cancellationToken)
    {
        if (!supplierId.HasValue)
        {
            return null;
        }

        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Id == supplierId.Value && x.IsActive, cancellationToken);
        return supplier ?? throw new AppProblemException(404, "supplier_not_found", "Supplier was not found.");
    }

    public static string NameOrFallback(Customer? customer, string fallback)
    {
        return customer?.Name ?? fallback.Trim();
    }

    public static string NameOrFallback(Supplier? supplier, string fallback)
    {
        return supplier?.Name ?? fallback.Trim();
    }
}

file static class OperationNumbers
{
    public static string Next(string prefix, DateTimeOffset now)
    {
        var token = Guid.CreateVersion7().ToString("N");
        return $"{prefix}-{now:yyyyMMdd}-{token[^8..].ToUpperInvariant()}";
    }
}

file static class OperationFingerprints
{
    public static object Shipment(CreateShipmentRequest request)
    {
        return new
        {
            request.SalesOrderId,
            RecipientName = request.RecipientName.Trim(),
            request.WarehouseId,
            TrackingNumber = OperationText.Optional(request.TrackingNumber),
            Notes = OperationText.Optional(request.Notes),
            Items = NormalizeItems(request.Items),
            request.CustomerId
        };
    }

    public static object Return(CreateReturnRequestRequest request)
    {
        return new
        {
            request.SalesOrderId,
            CustomerName = request.CustomerName.Trim(),
            request.WarehouseId,
            Reason = request.Reason.Trim(),
            Items = NormalizeItems(request.Items),
            request.CustomerId
        };
    }

    private static IReadOnlyList<OperationItemFingerprint> NormalizeItems(IReadOnlyList<OperationItemRequest> items)
    {
        return items
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(i => i.Quantity) })
            .OrderBy(x => x.ProductId)
            .Select(x => new OperationItemFingerprint(x.ProductId, x.Quantity))
            .ToList();
    }

    private sealed record OperationItemFingerprint(Guid ProductId, int Quantity);
}

file static class OperationStock
{
    public static async Task<IReadOnlyList<(Product Product, int Quantity)>> ResolveItemsAsync(
        StokioDbContext dbContext,
        IReadOnlyList<OperationItemRequest> requestItems,
        CancellationToken cancellationToken)
    {
        var normalizedItems = requestItems
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(i => i.Quantity) })
            .ToList();
        var productIds = normalizedItems.Select(x => x.ProductId).ToList();
        var products = await dbContext.Products
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (products.Count != productIds.Count)
        {
            throw new AppProblemException(404, "product_not_found", "One or more products were not found.");
        }

        return normalizedItems
            .Select(x => (products[x.ProductId], x.Quantity))
            .ToList();
    }

    public static async Task ApplyAsync(
        StokioDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        WarehouseStockLedger stockLedger,
        Product product,
        Guid? warehouseId,
        StockMovementType type,
        int quantity,
        string reason,
        CancellationToken cancellationToken)
    {
        var warehouseStock = await stockLedger.GetOrCreateStockAsync(product, warehouseId, cancellationToken);
        var previous = warehouseStock.Quantity;
        var next = type switch
        {
            StockMovementType.In => previous + quantity,
            StockMovementType.Out => previous - quantity,
            _ => throw new AppProblemException(400, "invalid_movement_type", "Invalid stock movement type.")
        };

        if (next < 0 || product.CurrentStock + next - previous < 0)
        {
            throw new AppProblemException(400, "insufficient_stock", "Stock cannot be reduced below zero.");
        }

        warehouseStock.Quantity = next;
        product.CurrentStock += next - previous;
        dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = currentTenant.TenantId,
            ProductId = product.Id,
            WarehouseId = warehouseStock.WarehouseId,
            Type = type,
            Quantity = quantity,
            PreviousQuantity = previous,
            NewQuantity = next,
            Reason = reason,
            PerformedByUserId = currentUser.UserId
        });
    }
}

file static class OperationMapper
{
    public static SalesOrderDto ToDto(SalesOrder order)
    {
        return new SalesOrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.CustomerName,
            order.WarehouseId,
            order.Warehouse?.Name,
            order.Status,
            order.Items.Count,
            order.Items.Sum(x => x.Quantity),
            order.Notes,
            order.CreatedAt,
            order.Items.Select(ToItemDto).ToList());
    }

    public static PurchaseRequestDto ToDto(PurchaseRequest request)
    {
        return new PurchaseRequestDto(
            request.Id,
            request.RequestNumber,
            request.SupplierId,
            request.SupplierName,
            request.WarehouseId,
            request.Warehouse?.Name,
            request.Status,
            request.Items.Count,
            request.Items.Sum(x => x.Quantity),
            request.Notes,
            request.ApprovedAt,
            request.ReceivedAt,
            request.CreatedAt,
            request.Items.Select(ToItemDto).ToList());
    }

    public static ShipmentDto ToDto(Shipment shipment)
    {
        return new ShipmentDto(
            shipment.Id,
            shipment.ShipmentNumber,
            shipment.SalesOrderId,
            shipment.SalesOrder?.OrderNumber,
            shipment.CustomerId,
            shipment.RecipientName,
            shipment.WarehouseId,
            shipment.Warehouse?.Name,
            shipment.TrackingNumber,
            shipment.Status,
            shipment.Items.Count,
            shipment.Items.Sum(x => x.Quantity),
            shipment.ShippedAt,
            shipment.CreatedAt,
            shipment.Items.Select(ToItemDto).ToList());
    }

    public static ReturnRequestDto ToDto(ReturnRequest request)
    {
        return new ReturnRequestDto(
            request.Id,
            request.ReturnNumber,
            request.SalesOrderId,
            request.SalesOrder?.OrderNumber,
            request.CustomerId,
            request.CustomerName,
            request.WarehouseId,
            request.Warehouse?.Name,
            request.Reason,
            request.Status,
            request.Items.Count,
            request.Items.Sum(x => x.Quantity),
            request.ReceivedAt,
            request.CreatedAt,
            request.Items.Select(ToItemDto).ToList());
    }

    public static object OrderSnapshot(SalesOrder order)
    {
        return new { order.Id, order.OrderNumber, order.CustomerId, order.CustomerName, order.WarehouseId, order.Status, Items = order.Items.Select(ToSnapshotItem) };
    }

    public static object PurchaseSnapshot(PurchaseRequest request)
    {
        return new { request.Id, request.RequestNumber, request.SupplierId, request.SupplierName, request.WarehouseId, request.Status, Items = request.Items.Select(ToSnapshotItem) };
    }

    public static object ShipmentSnapshot(Shipment shipment)
    {
        return new { shipment.Id, shipment.ShipmentNumber, shipment.SalesOrderId, shipment.CustomerId, shipment.RecipientName, shipment.WarehouseId, shipment.Status, Items = shipment.Items.Select(ToSnapshotItem) };
    }

    public static object ReturnSnapshot(ReturnRequest request)
    {
        return new { request.Id, request.ReturnNumber, request.SalesOrderId, request.CustomerId, request.CustomerName, request.WarehouseId, request.Status, Items = request.Items.Select(ToSnapshotItem) };
    }

    private static OperationItemDto ToItemDto(SalesOrderItem item)
    {
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity);
    }

    private static OperationItemDto ToItemDto(PurchaseRequestItem item)
    {
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity);
    }

    private static OperationItemDto ToItemDto(ShipmentItem item)
    {
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity);
    }

    private static OperationItemDto ToItemDto(ReturnRequestItem item)
    {
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity);
    }

    private static object ToSnapshotItem(SalesOrderItem item)
    {
        return new { item.ProductId, item.Quantity };
    }

    private static object ToSnapshotItem(PurchaseRequestItem item)
    {
        return new { item.ProductId, item.Quantity };
    }

    private static object ToSnapshotItem(ShipmentItem item)
    {
        return new { item.ProductId, item.Quantity };
    }

    private static object ToSnapshotItem(ReturnRequestItem item)
    {
        return new { item.ProductId, item.Quantity };
    }
}
