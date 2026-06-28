using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Stock;

public sealed class OperationServicesTests
{
    [Fact]
    public async Task SalesOrderNumbers_AreUnique_WhenCreatedQuickly()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "ORDER-1",
            Name = "Order Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var first = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", null, null, [new OperationItemRequest(product.Id, 1)]), CancellationToken.None);
        var second = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", null, null, [new OperationItemRequest(product.Id, 1)]), CancellationToken.None);

        Assert.NotEqual(first.OrderNumber, second.OrderNumber);
        Assert.Matches(@"^SO-\d{8}-[0-9A-F]{8}$", first.OrderNumber);
        Assert.Matches(@"^SO-\d{8}-[0-9A-F]{8}$", second.OrderNumber);
    }

    [Fact]
    public async Task Shipment_Return_And_PurchaseReceipt_UpdateWarehouseStock()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var shipmentService = new ShipmentService(dbContext, tenant, user, clock, ledger, auditWriter);
        var returnService = new ReturnRequestService(dbContext, tenant, user, clock, ledger, auditWriter);
        var purchaseService = new PurchaseRequestService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "OPS-1",
            Name = "Operation Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);

        await shipmentService.CreateAsync(new CreateShipmentRequest(null, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 3)]), CancellationToken.None);
        await returnService.CreateAsync(new CreateReturnRequestRequest(null, "Customer", warehouse.Id, "Damaged package", [new OperationItemRequest(product.Id, 2)]), CancellationToken.None);
        var purchase = await purchaseService.CreateAsync(new CreatePurchaseRequestRequest("Supplier", warehouse.Id, null, [new OperationItemRequest(product.Id, 5)]), CancellationToken.None);
        await purchaseService.ReceiveAsync(purchase.Id, CancellationToken.None);

        var storedProduct = dbContext.Products.Single(x => x.Id == product.Id);
        var storedWarehouseStock = dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id);
        Assert.Equal(14, storedProduct.CurrentStock);
        Assert.Equal(14, storedWarehouseStock.Quantity);
        Assert.Contains(dbContext.StockMovements, x => x.Type == StockMovementType.Out && x.Quantity == 3);
        Assert.Equal(4, dbContext.StockMovements.Count(x => x.ProductId == product.Id));
    }

    [Fact]
    public async Task ShipmentCreate_ReturnsExistingShipment_WhenIdempotencyKeyIsReused()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var shipmentService = new ShipmentService(
            dbContext,
            tenant,
            user,
            clock,
            ledger,
            auditWriter,
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("shipment-1")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SHIP-IDEMP-1",
            Name = "Shipment Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var request = new CreateShipmentRequest(null, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 3)]);

        var first = await shipmentService.CreateAsync(request, CancellationToken.None);
        var second = await shipmentService.CreateAsync(request, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(7, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Equal(7, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);
        Assert.Single(dbContext.Shipments);
        Assert.Single(dbContext.StockMovements.Where(x => x.Type == StockMovementType.Out && x.ProductId == product.Id));
    }

    [Fact]
    public async Task ShipmentCreate_TracksPartialAndFullShipment_ForLinkedSalesOrder()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var shipmentService = new ShipmentService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SHIP-ORDER-1",
            Name = "Linked Shipment Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var order = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", warehouse.Id, null, [new OperationItemRequest(product.Id, 5)]), CancellationToken.None);

        await shipmentService.CreateAsync(new CreateShipmentRequest(order.Id, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 2)]), CancellationToken.None);

        var storedOrder = dbContext.SalesOrders.Include(x => x.Items).Single(x => x.Id == order.Id);
        var orderItem = Assert.Single(storedOrder.Items);
        Assert.Equal(SalesOrderStatus.PartiallyShipped, storedOrder.Status);
        Assert.Equal(2, orderItem.ShippedQuantity);
        Assert.Equal(8, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);

        await shipmentService.CreateAsync(new CreateShipmentRequest(order.Id, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 3)]), CancellationToken.None);

        Assert.Equal(SalesOrderStatus.Shipped, storedOrder.Status);
        Assert.Equal(5, orderItem.ShippedQuantity);
        Assert.Equal(0, orderItem.ReturnedQuantity);
        Assert.Equal(5, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);
    }

    [Fact]
    public async Task ShipmentCreate_RejectsOverShipment_ForLinkedSalesOrder()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var shipmentService = new ShipmentService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SHIP-ORDER-OVER",
            Name = "Over Shipment Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var order = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", warehouse.Id, null, [new OperationItemRequest(product.Id, 3)]), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            shipmentService.CreateAsync(new CreateShipmentRequest(order.Id, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 4)]), CancellationToken.None));

        Assert.Equal("shipment_quantity_exceeds_order_remaining", exception.Code);
        Assert.Equal(10, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);
        Assert.Equal(0, dbContext.SalesOrderItems.Single(x => x.SalesOrderId == order.Id).ShippedQuantity);
        Assert.Empty(dbContext.Shipments);
        Assert.DoesNotContain(dbContext.StockMovements, x => x.Type == StockMovementType.Out && x.ProductId == product.Id);
    }

    [Fact]
    public async Task PurchaseReceive_ReturnsExistingReceipt_WhenIdempotencyKeyIsReused()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var purchaseService = new PurchaseRequestService(
            dbContext,
            tenant,
            user,
            clock,
            ledger,
            auditWriter,
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("purchase-receive-1")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "PUR-IDEMP-1",
            Name = "Purchase Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        var purchase = await purchaseService.CreateAsync(new CreatePurchaseRequestRequest("Supplier", null, null, [new OperationItemRequest(product.Id, 5)]), CancellationToken.None);

        var first = await purchaseService.ReceiveAsync(purchase.Id, CancellationToken.None);
        var second = await purchaseService.ReceiveAsync(purchase.Id, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(PurchaseRequestStatus.Received, second.Status);
        Assert.Equal(5, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Single(dbContext.StockMovements.Where(x => x.Type == StockMovementType.In && x.ProductId == product.Id));
        Assert.Equal(IdempotencyRecordStatus.Completed, Assert.Single(dbContext.IdempotencyRecords).Status);
    }

    [Fact]
    public async Task ReturnCreate_ReturnsExistingReturn_WhenIdempotencyKeyIsReused()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var returnService = new ReturnRequestService(
            dbContext,
            tenant,
            user,
            clock,
            ledger,
            auditWriter,
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("return-1")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "RET-IDEMP-1",
            Name = "Return Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        var request = new CreateReturnRequestRequest(null, "Customer", null, "Damaged package", [new OperationItemRequest(product.Id, 2)]);

        var first = await returnService.CreateAsync(request, CancellationToken.None);
        var second = await returnService.CreateAsync(request, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Single(dbContext.ReturnRequests);
        Assert.Single(dbContext.StockMovements.Where(x => x.Type == StockMovementType.In && x.ProductId == product.Id));
        Assert.Equal(IdempotencyRecordStatus.Completed, Assert.Single(dbContext.IdempotencyRecords).Status);
    }

    [Fact]
    public async Task ReturnCreate_UpdatesReturnedQuantity_ForLinkedSalesOrder()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var shipmentService = new ShipmentService(dbContext, tenant, user, clock, ledger, auditWriter);
        var returnService = new ReturnRequestService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "RET-ORDER-1",
            Name = "Linked Return Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var order = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", warehouse.Id, null, [new OperationItemRequest(product.Id, 5)]), CancellationToken.None);
        await shipmentService.CreateAsync(new CreateShipmentRequest(order.Id, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 2)]), CancellationToken.None);

        await returnService.CreateAsync(new CreateReturnRequestRequest(order.Id, "Customer", warehouse.Id, "Damaged package", [new OperationItemRequest(product.Id, 1)]), CancellationToken.None);

        var orderItem = dbContext.SalesOrderItems.Single(x => x.SalesOrderId == order.Id);
        Assert.Equal(2, orderItem.ShippedQuantity);
        Assert.Equal(1, orderItem.ReturnedQuantity);
        Assert.Equal(9, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);
    }

    [Fact]
    public async Task ReturnCreate_RejectsReturnAboveShippedQuantity_ForLinkedSalesOrder()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var shipmentService = new ShipmentService(dbContext, tenant, user, clock, ledger, auditWriter);
        var returnService = new ReturnRequestService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "RET-ORDER-OVER",
            Name = "Over Return Product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "seed"), CancellationToken.None);
        var warehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var order = await orderService.CreateAsync(new CreateSalesOrderRequest("Customer", warehouse.Id, null, [new OperationItemRequest(product.Id, 5)]), CancellationToken.None);
        await shipmentService.CreateAsync(new CreateShipmentRequest(order.Id, "Customer", warehouse.Id, null, null, [new OperationItemRequest(product.Id, 2)]), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            returnService.CreateAsync(new CreateReturnRequestRequest(order.Id, "Customer", warehouse.Id, "Damaged package", [new OperationItemRequest(product.Id, 3)]), CancellationToken.None));

        Assert.Equal("return_quantity_exceeds_shipped_remaining", exception.Code);
        Assert.Equal(0, dbContext.SalesOrderItems.Single(x => x.SalesOrderId == order.Id).ReturnedQuantity);
        Assert.Equal(8, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id && x.WarehouseId == warehouse.Id).Quantity);
        Assert.Empty(dbContext.ReturnRequests);
        Assert.Single(dbContext.StockMovements.Where(x => x.Type == StockMovementType.Out && x.ProductId == product.Id));
    }

    private static StokioDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StokioDbContext(options, new TestCurrentTenant(tenantId));
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public bool HasTenant => true;
        public Guid TenantId => tenantId;
        public string? TenantSlug => "test";
        public void SetTenant(Guid tenantId, string? slug)
        {
        }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public string? Email => "owner@test.local";
        public string? Role => UserRole.Owner.ToString();
    }

    private sealed class TestIdempotencyKeyAccessor(string key) : IIdempotencyKeyAccessor
    {
        public string? IdempotencyKey => key;
    }
}
