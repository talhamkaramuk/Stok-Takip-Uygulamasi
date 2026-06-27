using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
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
