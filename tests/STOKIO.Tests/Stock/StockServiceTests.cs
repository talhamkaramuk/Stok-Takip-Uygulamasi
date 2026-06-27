using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Application.Dtos.Warehouses;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Stock;

public sealed class StockServiceTests
{
    [Fact]
    public async Task CreateMovementAsync_BlocksNegativeStock()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(dbContext, tenant, user, new WarehouseStockLedger(dbContext, tenant), auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-1",
            Name = "Cable",
            CurrentStock = 3,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var request = new CreateStockMovementRequest(product.Id, StockMovementType.Out, 4, "sale");

        var exception = await Assert.ThrowsAsync<AppProblemException>(() => service.CreateMovementAsync(request, CancellationToken.None));
        Assert.Equal("insufficient_stock", exception.Code);
    }

    [Fact]
    public async Task CreateMovementAsync_AppliesCountCorrectionAsExactStock()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(dbContext, tenant, user, new WarehouseStockLedger(dbContext, tenant), auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-2",
            Name = "Case",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 3, "initial"), CancellationToken.None);
        var movement = await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.CountCorrection, 7, "count"), CancellationToken.None);

        Assert.Equal(3, movement.PreviousQuantity);
        Assert.Equal(7, movement.NewQuantity);
        Assert.Equal(7, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
    }

    [Fact]
    public async Task CheckConsistencyAsync_ReturnsConsistent_WhenMovementsMatchProductStock()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(dbContext, tenant, user, new WarehouseStockLedger(dbContext, tenant), auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-3",
            Name = "Adapter",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "purchase"), CancellationToken.None);
        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.Out, 2, "sale"), CancellationToken.None);

        var consistency = await service.CheckConsistencyAsync(CancellationToken.None);

        var item = Assert.Single(consistency);
        Assert.True(item.IsConsistent);
        Assert.Equal(8, item.LedgerCurrentStock);
        Assert.Empty(item.Issues);
    }

    [Fact]
    public async Task CreateMovementAsync_ReturnsExistingMovement_WhenIdempotencyKeyIsReused()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(
            dbContext,
            tenant,
            user,
            ledger,
            auditWriter,
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("stock-in-1")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-IDEMP-1",
            Name = "Idempotent Cable",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        var request = new CreateStockMovementRequest(product.Id, StockMovementType.In, 5, "seed");

        var first = await service.CreateMovementAsync(request, CancellationToken.None);
        var second = await service.CreateMovementAsync(request, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(5, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Single(dbContext.StockMovements.Where(x => x.ProductId == product.Id));
        Assert.Single(dbContext.IdempotencyRecords);
    }

    [Fact]
    public async Task CreateMovementAsync_RejectsSameIdempotencyKeyWithDifferentPayload()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(
            dbContext,
            tenant,
            user,
            ledger,
            auditWriter,
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("stock-in-conflict")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-IDEMP-2",
            Name = "Idempotent Adapter",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 5, "seed"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 6, "seed"), CancellationToken.None));

        Assert.Equal("idempotency_key_conflict", exception.Code);
        Assert.Equal(5, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
    }

    [Fact]
    public async Task CreateMovementAsync_IncrementsConcurrencyVersions_WhenStockRowsChange()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new StockService(dbContext, tenant, user, new WarehouseStockLedger(dbContext, tenant), auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-VERSION-1",
            Name = "Versioned Stock",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 5, "seed"), CancellationToken.None);
        await service.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.Out, 2, "sale"), CancellationToken.None);

        var storedProduct = dbContext.Products.Single(x => x.Id == product.Id);
        var storedWarehouseStock = dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id);
        Assert.Equal(3, storedProduct.CurrentStock);
        Assert.True(storedProduct.Version > 1);
        Assert.True(storedWarehouseStock.Version > 1);
    }

    [Fact]
    public async Task TransferAsync_MovesStockBetweenWarehouses_WithoutChangingTotalStock()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var user = new TestCurrentUser();
        var tenant = new TestCurrentTenant(tenantId);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var warehouseService = new WarehouseService(dbContext, tenant, user, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-4",
            Name = "Powerbank",
            CurrentStock = 0,
            CriticalStockLevel = 2
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 10, "purchase"), CancellationToken.None);
        var mainWarehouse = dbContext.Warehouses.Single(x => x.IsDefault);
        var branchWarehouse = await warehouseService.CreateAsync(new CreateWarehouseRequest("BR-1", "Şube Depo", null, false), CancellationToken.None);

        await warehouseService.TransferAsync(new StockTransferRequest(product.Id, mainWarehouse.Id, branchWarehouse.Id, 4, "branch replenishment"), CancellationToken.None);

        var stocks = dbContext.WarehouseStocks.Where(x => x.ProductId == product.Id).ToDictionary(x => x.WarehouseId, x => x.Quantity);
        Assert.Equal(10, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Equal(6, stocks[mainWarehouse.Id]);
        Assert.Equal(4, stocks[branchWarehouse.Id]);
        Assert.Contains(dbContext.StockMovements, x => x.Type == StockMovementType.TransferOut && x.WarehouseId == mainWarehouse.Id);
        Assert.Contains(dbContext.StockMovements, x => x.Type == StockMovementType.TransferIn && x.WarehouseId == branchWarehouse.Id);
    }

    private static StokioDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StokioDbContext(options, new TestCurrentTenant(tenantId));
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
