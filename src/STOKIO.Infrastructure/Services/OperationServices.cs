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
    public async Task<PagedResult<SalesOrderDto>> ListAsync(string? search, SalesOrderStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.OrderNumber.ToLower().Contains(term) ||
                x.CustomerName.ToLower().Contains(term) ||
                (x.Warehouse != null && x.Warehouse.Name.ToLower().Contains(term)));
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
    DbTransactionRunner? transactionRunner = null,
    IMetricsRecorder? metricsRecorder = null) : IPurchaseRequestService
{
    public async Task<PagedResult<PurchaseRequestDto>> ListAsync(string? search, PurchaseRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.RequestNumber.ToLower().Contains(term) ||
                x.SupplierName.ToLower().Contains(term) ||
                (x.Warehouse != null && x.Warehouse.Name.ToLower().Contains(term)));
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

    public async Task<PurchaseRequestDto> ReceiveAsync(Guid id, ReceivePurchaseRequestRequest? request, CancellationToken cancellationToken)
    {
        OperationGuards.EnsureTenant(currentTenant);
        const string operationName = "purchase_request.receive";
        var requestFingerprint = OperationFingerprints.PurchaseReceive(id, request);

        return await TransactionRunner.RunAsync(async ct =>
        {
            var existing = await Idempotency.TryReserveAsync(operationName, requestFingerprint, ct);
            if (existing is not null)
            {
                return Idempotency.TryReadResponseSnapshot<PurchaseRequestDto>(existing)
                    ?? await FindDtoByIdempotencyRecordAsync(existing, ct);
            }

            var purchaseRequest = await FindAsync(id, ct);
            if (purchaseRequest.Status is PurchaseRequestStatus.Received or PurchaseRequestStatus.Cancelled)
            {
                throw new AppProblemException(400, "purchase_request_closed", "Kapalı alım talepleri teslim alınamaz.");
            }

            if (purchaseRequest.Status is not (PurchaseRequestStatus.Approved or PurchaseRequestStatus.PartiallyReceived))
            {
                throw new AppProblemException(409, "purchase_request_not_approved", "Alım talebi teslim alınmadan önce onaylanmalıdır.");
            }

            var receivedLines = OperationPurchaseReceiving.ResolveReceivedLines(purchaseRequest, request);
            var receivedItems = receivedLines
                .GroupBy(x => x.Item.ProductId)
                .Select(x => (x.First().Item.Product, Quantity: x.Sum(i => i.Quantity)))
                .OrderBy(x => x.Product.Id)
                .ToList();
            var warehouseStocks = await OperationStock.PrepareStocksForWriteAsync(stockLedger, receivedItems, purchaseRequest.WarehouseId, ct);

            purchaseRequest.ApprovedAt ??= clock.UtcNow;
            var stockMetricEvents = new List<(StockMovementType Type, int Quantity, bool IsCritical)>();
            foreach (var item in receivedItems)
            {
                OperationStock.ApplyPrepared(
                    dbContext,
                    currentTenant,
                    currentUser,
                    item.Product,
                    warehouseStocks[item.Product.Id],
                    StockMovementType.In,
                    item.Quantity,
                    $"Purchase request received: {purchaseRequest.RequestNumber}");
                stockMetricEvents.Add((StockMovementType.In, item.Quantity, item.Product.CurrentStock <= item.Product.CriticalStockLevel));
            }

            foreach (var line in receivedLines)
            {
                line.Item.ReceivedQuantity += line.Quantity;
            }

            purchaseRequest.Status = purchaseRequest.Items.All(x => x.ReceivedQuantity >= x.Quantity)
                ? PurchaseRequestStatus.Received
                : PurchaseRequestStatus.PartiallyReceived;
            purchaseRequest.ReceivedAt = purchaseRequest.Status == PurchaseRequestStatus.Received
                ? clock.UtcNow
                : null;

            auditWriter.AddSnapshot("purchase_request.received", nameof(PurchaseRequest), purchaseRequest.Id, null, OperationMapper.PurchaseSnapshot(purchaseRequest));
            await dbContext.SaveChangesAsync(ct);
            var dto = OperationMapper.ToDto(purchaseRequest);

            if (await Idempotency.CompleteAsync(operationName, requestFingerprint, nameof(PurchaseRequest), purchaseRequest.Id.ToString(), dto, ct))
            {
                await dbContext.SaveChangesAsync(ct);
            }

            foreach (var metricEvent in stockMetricEvents)
            {
                metricsRecorder?.RecordStockMovement(metricEvent.Type, metricEvent.Quantity, metricEvent.IsCritical);
            }

            return dto;
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
    DbTransactionRunner? transactionRunner = null,
    IMetricsRecorder? metricsRecorder = null) : IShipmentService
{
    public async Task<PagedResult<ShipmentDto>> ListAsync(string? search, ShipmentStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.ShipmentNumber.ToLower().Contains(term) ||
                x.RecipientName.ToLower().Contains(term) ||
                (x.TrackingNumber != null && x.TrackingNumber.ToLower().Contains(term)) ||
                (x.Warehouse != null && x.Warehouse.Name.ToLower().Contains(term)) ||
                (x.SalesOrder != null && x.SalesOrder.OrderNumber.ToLower().Contains(term)));
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

        return await TransactionRunner.RunAsync(async ct =>
        {
            var existing = await Idempotency.TryReserveAsync(operationName, requestFingerprint, ct);
            if (existing is not null)
            {
                return Idempotency.TryReadResponseSnapshot<ShipmentDto>(existing)
                    ?? await FindDtoByIdempotencyRecordAsync(existing, ct);
            }

            var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, ct);
            var order = request.SalesOrderId.HasValue
                ? await dbContext.SalesOrders
                    .Include(x => x.Items)
                    .SingleOrDefaultAsync(x => x.Id == request.SalesOrderId.Value, ct)
                : null;
            if (request.SalesOrderId.HasValue && order is null)
            {
                throw new AppProblemException(404, "sales_order_not_found", "Sales order was not found.");
            }

            var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, ct);
            OperationOrderFulfillment.EnsureCanShip(order, items);
            var warehouseStocks = await OperationStock.PrepareStocksForWriteAsync(stockLedger, items, warehouse.Id, ct);
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

            var stockMetricEvents = new List<(StockMovementType Type, int Quantity, bool IsCritical)>();
            foreach (var item in items)
            {
                OperationStock.ApplyPrepared(
                    dbContext,
                    currentTenant,
                    currentUser,
                    item.Product,
                    warehouseStocks[item.Product.Id],
                    StockMovementType.Out,
                    item.Quantity,
                    $"Shipment created: {shipment.ShipmentNumber}");
                stockMetricEvents.Add((StockMovementType.Out, item.Quantity, item.Product.CurrentStock <= item.Product.CriticalStockLevel));

                shipment.Items.Add(new ShipmentItem
                {
                    TenantId = currentTenant.TenantId,
                    ProductId = item.Product.Id,
                    Product = item.Product,
                    Quantity = item.Quantity
                });

                OperationOrderFulfillment.ApplyShipment(order, item.Product.Id, item.Quantity);
            }

            OperationOrderFulfillment.RefreshShipmentStatus(order);

            dbContext.Shipments.Add(shipment);
            auditWriter.AddSnapshot("shipment.created", nameof(Shipment), shipment.Id, null, OperationMapper.ShipmentSnapshot(shipment));
            await dbContext.SaveChangesAsync(ct);
            var dto = OperationMapper.ToDto(shipment);

            if (await Idempotency.CompleteAsync(operationName, requestFingerprint, nameof(Shipment), shipment.Id.ToString(), dto, ct))
            {
                await dbContext.SaveChangesAsync(ct);
            }

            foreach (var metricEvent in stockMetricEvents)
            {
                metricsRecorder?.RecordStockMovement(metricEvent.Type, metricEvent.Quantity, metricEvent.IsCritical);
            }

            return dto;
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
    DbTransactionRunner? transactionRunner = null,
    IMetricsRecorder? metricsRecorder = null) : IReturnRequestService
{
    public async Task<PagedResult<ReturnRequestDto>> ListAsync(string? search, ReturnRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.ReturnNumber.ToLower().Contains(term) ||
                x.CustomerName.ToLower().Contains(term) ||
                x.Reason.ToLower().Contains(term) ||
                (x.Warehouse != null && x.Warehouse.Name.ToLower().Contains(term)) ||
                (x.SalesOrder != null && x.SalesOrder.OrderNumber.ToLower().Contains(term)));
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

        return await TransactionRunner.RunAsync(async ct =>
        {
            var existing = await Idempotency.TryReserveAsync(operationName, requestFingerprint, ct);
            if (existing is not null)
            {
                return Idempotency.TryReadResponseSnapshot<ReturnRequestDto>(existing)
                    ?? await FindDtoByIdempotencyRecordAsync(existing, ct);
            }

            var warehouse = await stockLedger.ResolveWarehouseAsync(request.WarehouseId, ct);
            var order = request.SalesOrderId.HasValue
                ? await dbContext.SalesOrders
                    .Include(x => x.Items)
                    .SingleOrDefaultAsync(x => x.Id == request.SalesOrderId.Value, ct)
                : null;
            if (request.SalesOrderId.HasValue && order is null)
            {
                throw new AppProblemException(404, "sales_order_not_found", "Sales order was not found.");
            }

            var items = await OperationStock.ResolveItemsAsync(dbContext, request.Items, ct);
            OperationOrderFulfillment.EnsureCanReturn(order, items);
            var warehouseStocks = await OperationStock.PrepareStocksForWriteAsync(stockLedger, items, warehouse.Id, ct);
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

            var stockMetricEvents = new List<(StockMovementType Type, int Quantity, bool IsCritical)>();
            foreach (var item in items)
            {
                OperationStock.ApplyPrepared(
                    dbContext,
                    currentTenant,
                    currentUser,
                    item.Product,
                    warehouseStocks[item.Product.Id],
                    StockMovementType.In,
                    item.Quantity,
                    $"Return received: {returnRequest.ReturnNumber}");
                stockMetricEvents.Add((StockMovementType.In, item.Quantity, item.Product.CurrentStock <= item.Product.CriticalStockLevel));

                returnRequest.Items.Add(new ReturnRequestItem
                {
                    TenantId = currentTenant.TenantId,
                    ProductId = item.Product.Id,
                    Product = item.Product,
                    Quantity = item.Quantity
                });

                OperationOrderFulfillment.ApplyReturn(order, item.Product.Id, item.Quantity);
            }

            dbContext.ReturnRequests.Add(returnRequest);
            auditWriter.AddSnapshot("return_request.created", nameof(ReturnRequest), returnRequest.Id, null, OperationMapper.ReturnSnapshot(returnRequest));
            await dbContext.SaveChangesAsync(ct);
            var dto = OperationMapper.ToDto(returnRequest);

            if (await Idempotency.CompleteAsync(operationName, requestFingerprint, nameof(ReturnRequest), returnRequest.Id.ToString(), dto, ct))
            {
                await dbContext.SaveChangesAsync(ct);
            }

            foreach (var metricEvent in stockMetricEvents)
            {
                metricsRecorder?.RecordStockMovement(metricEvent.Type, metricEvent.Quantity, metricEvent.IsCritical);
            }

            return dto;
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

file static class OperationOrderFulfillment
{
    public static void EnsureCanShip(SalesOrder? order, IReadOnlyList<(Product Product, int Quantity)> items)
    {
        if (order is null)
        {
            return;
        }

        if (order.Status is SalesOrderStatus.Draft or SalesOrderStatus.Cancelled)
        {
            throw new AppProblemException(409, "sales_order_not_shippable", "Sipariş sevkiyata uygun durumda değil.");
        }

        foreach (var item in items)
        {
            var remaining = order.Items
                .Where(x => x.ProductId == item.Product.Id)
                .Sum(x => x.Quantity - x.ShippedQuantity);

            if (remaining == 0 && order.Items.All(x => x.ProductId != item.Product.Id))
            {
                throw new AppProblemException(400, "shipment_item_not_ordered", "Sevkiyat kalemi bağlı siparişte bulunmuyor.");
            }

            if (item.Quantity > remaining)
            {
                throw new AppProblemException(400, "shipment_quantity_exceeds_order_remaining", "Sevkiyat miktarı siparişte kalan sevk edilebilir miktarı aşıyor.");
            }
        }
    }

    public static void ApplyShipment(SalesOrder? order, Guid productId, int quantity)
    {
        if (order is null)
        {
            return;
        }

        var remaining = quantity;
        foreach (var orderItem in order.Items.Where(x => x.ProductId == productId).OrderBy(x => x.Id))
        {
            var available = orderItem.Quantity - orderItem.ShippedQuantity;
            var applied = Math.Min(available, remaining);
            orderItem.ShippedQuantity += applied;
            remaining -= applied;

            if (remaining == 0)
            {
                return;
            }
        }
    }

    public static void RefreshShipmentStatus(SalesOrder? order)
    {
        if (order is null)
        {
            return;
        }

        order.Status = order.Items.All(x => x.ShippedQuantity >= x.Quantity)
            ? SalesOrderStatus.Shipped
            : SalesOrderStatus.PartiallyShipped;
    }

    public static void EnsureCanReturn(SalesOrder? order, IReadOnlyList<(Product Product, int Quantity)> items)
    {
        if (order is null)
        {
            return;
        }

        if (order.Status is SalesOrderStatus.Draft or SalesOrderStatus.Cancelled)
        {
            throw new AppProblemException(409, "sales_order_not_returnable", "Sipariş iade almaya uygun durumda değil.");
        }

        foreach (var item in items)
        {
            var returnable = order.Items
                .Where(x => x.ProductId == item.Product.Id)
                .Sum(x => x.ShippedQuantity - x.ReturnedQuantity);

            if (returnable == 0 && order.Items.All(x => x.ProductId != item.Product.Id))
            {
                throw new AppProblemException(400, "return_item_not_ordered", "İade kalemi bağlı siparişte bulunmuyor.");
            }

            if (item.Quantity > returnable)
            {
                throw new AppProblemException(400, "return_quantity_exceeds_shipped_remaining", "İade miktarı sevk edilmiş ve iade edilmemiş miktarı aşıyor.");
            }
        }
    }

    public static void ApplyReturn(SalesOrder? order, Guid productId, int quantity)
    {
        if (order is null)
        {
            return;
        }

        var remaining = quantity;
        foreach (var orderItem in order.Items.Where(x => x.ProductId == productId).OrderBy(x => x.Id))
        {
            var available = orderItem.ShippedQuantity - orderItem.ReturnedQuantity;
            var applied = Math.Min(available, remaining);
            orderItem.ReturnedQuantity += applied;
            remaining -= applied;

            if (remaining == 0)
            {
                return;
            }
        }
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
    public static object PurchaseReceive(Guid purchaseRequestId, ReceivePurchaseRequestRequest? request)
    {
        return new
        {
            PurchaseRequestId = purchaseRequestId,
            Items = request?.Items is null ? null : NormalizeItems(request.Items)
        };
    }

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

file static class OperationPurchaseReceiving
{
    public static IReadOnlyList<(PurchaseRequestItem Item, int Quantity)> ResolveReceivedLines(
        PurchaseRequest request,
        ReceivePurchaseRequestRequest? receiveRequest)
    {
        if (receiveRequest?.Items is null)
        {
            var remainingLines = request.Items
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.Id)
                .Select(x => (Item: x, Quantity: x.Quantity - x.ReceivedQuantity))
                .Where(x => x.Quantity > 0)
                .ToList();

            return remainingLines.Count == 0
                ? throw new AppProblemException(400, "purchase_request_closed", "Alım talebinde teslim alınacak kalan miktar yok.")
                : remainingLines;
        }

        var normalizedItems = receiveRequest.Items
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(i => i.Quantity) })
            .OrderBy(x => x.ProductId)
            .ToList();
        if (normalizedItems.Count == 0 || normalizedItems.Any(x => x.Quantity <= 0))
        {
            throw new AppProblemException(400, "purchase_receive_items_required", "Teslim alma için en az bir pozitif miktarlı kalem girilmelidir.");
        }

        var receivedLines = new List<(PurchaseRequestItem Item, int Quantity)>();

        foreach (var item in normalizedItems)
        {
            var requestItems = request.Items
                .Where(x => x.ProductId == item.ProductId)
                .OrderBy(x => x.Id)
                .ToList();
            if (requestItems.Count == 0)
            {
                throw new AppProblemException(400, "purchase_receive_item_not_requested", "Teslim alınan ürün alım talebinde bulunmuyor.");
            }

            var remainingTotal = requestItems.Sum(x => x.Quantity - x.ReceivedQuantity);
            if (item.Quantity > remainingTotal)
            {
                throw new AppProblemException(400, "purchase_receive_quantity_exceeds_remaining", "Teslim alma miktarı talepte kalan miktarı aşıyor.");
            }

            var remaining = item.Quantity;
            foreach (var requestItem in requestItems)
            {
                var available = requestItem.Quantity - requestItem.ReceivedQuantity;
                var applied = Math.Min(available, remaining);
                if (applied > 0)
                {
                    receivedLines.Add((requestItem, applied));
                    remaining -= applied;
                }

                if (remaining == 0)
                {
                    break;
                }
            }
        }

        return receivedLines;
    }
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
            .OrderBy(x => x.ProductId)
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
        var warehouseStocks = await PrepareStocksForWriteAsync(stockLedger, [(product, quantity)], warehouseId, cancellationToken);
        ApplyPrepared(
            dbContext,
            currentTenant,
            currentUser,
            product,
            warehouseStocks[product.Id],
            type,
            quantity,
            reason);
    }

    public static async Task<IReadOnlyDictionary<Guid, WarehouseStock>> PrepareStocksForWriteAsync(
        WarehouseStockLedger stockLedger,
        IReadOnlyList<(Product Product, int Quantity)> items,
        Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var orderedItems = items
            .OrderBy(x => x.Product.Id)
            .ToList();
        var warehouseStocks = new Dictionary<Guid, WarehouseStock>();
        foreach (var item in orderedItems)
        {
            warehouseStocks[item.Product.Id] = await stockLedger.GetOrCreateStockAsync(item.Product, warehouseId, cancellationToken);
        }

        await stockLedger.LockForStockWriteAsync(
            orderedItems.Select(x => x.Product).ToList(),
            warehouseStocks.Values.ToList(),
            cancellationToken);

        return warehouseStocks;
    }

    public static void ApplyPrepared(
        StokioDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        Product product,
        WarehouseStock warehouseStock,
        StockMovementType type,
        int quantity,
        string reason)
    {
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
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity, item.ShippedQuantity, item.ReturnedQuantity);
    }

    private static OperationItemDto ToItemDto(PurchaseRequestItem item)
    {
        return new OperationItemDto(item.ProductId, item.Product.Sku, item.Product.Name, item.Quantity, ReceivedQuantity: item.ReceivedQuantity);
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
        return new { item.ProductId, item.Quantity, item.ShippedQuantity, item.ReturnedQuantity };
    }

    private static object ToSnapshotItem(PurchaseRequestItem item)
    {
        return new { item.ProductId, item.Quantity, item.ReceivedQuantity };
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
