using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Counts;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Stock;

public sealed class InventoryCountServiceTests
{
    [Fact]
    public async Task CreateAsync_UsesDefaultWarehouse_WhenWarehouseIsNotSelected()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var service = new InventoryCountService(
            dbContext,
            tenant,
            user,
            new TestClock(),
            ledger,
            new AuditWriter(dbContext, tenant, user));
        dbContext.Products.Add(new Product
        {
            TenantId = tenantId,
            Sku = "SKU-COUNT-1",
            Name = "Counted product",
            CurrentStock = 5,
            CriticalStockLevel = 1
        });
        await dbContext.SaveChangesAsync();

        var count = await service.CreateAsync(new CreateInventoryCountRequest("Daily count"), CancellationToken.None);

        var warehouse = Assert.Single(dbContext.Warehouses);
        Assert.Equal(warehouse.Id, count.WarehouseId);
        Assert.Equal("Ana Depo", count.WarehouseName);
        Assert.Single(dbContext.WarehouseStocks.Where(x => x.WarehouseId == warehouse.Id && x.Quantity == 5));
    }

    [Fact]
    public async Task CloseAsync_ReturnsExistingCount_WhenIdempotencyKeyIsReused()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var service = new InventoryCountService(
            dbContext,
            tenant,
            user,
            new TestClock(),
            ledger,
            new AuditWriter(dbContext, tenant, user),
            new IdempotencyService(dbContext, tenant, new TestIdempotencyKeyAccessor("count-close-1")),
            new DbTransactionRunner(dbContext));
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-COUNT-IDEMP-1",
            Name = "Counted product",
            CurrentStock = 5,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        dbContext.ProductBarcodes.Add(new ProductBarcode
        {
            TenantId = tenantId,
            Product = product,
            ProductId = product.Id,
            Barcode = "8690000000001"
        });
        await dbContext.SaveChangesAsync();
        var count = await service.CreateAsync(new CreateInventoryCountRequest("Daily count"), CancellationToken.None);
        await service.ScanAsync(count.Id, new ScanCountItemRequest("8690000000001", 7), CancellationToken.None);

        var first = await service.CloseAsync(count.Id, new CloseInventoryCountRequest(true), CancellationToken.None);
        var second = await service.CloseAsync(count.Id, new CloseInventoryCountRequest(true), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(InventoryCountStatus.Closed, second.Status);
        Assert.Equal(7, dbContext.Products.Single(x => x.Id == product.Id).CurrentStock);
        Assert.Equal(7, dbContext.WarehouseStocks.Single(x => x.ProductId == product.Id).Quantity);
        Assert.Single(dbContext.StockMovements.Where(x => x.Type == StockMovementType.CountCorrection && x.ProductId == product.Id));
    }

    [Fact]
    public async Task GetAsync_Warns_WhenWarehouseMovementOccursAfterSnapshot()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var service = new InventoryCountService(
            dbContext,
            tenant,
            user,
            new TestClock(),
            ledger,
            auditWriter);
        var stockService = new StockService(dbContext, tenant, user, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-COUNT-WARN-1",
            Name = "Warning product",
            CurrentStock = 5,
            CriticalStockLevel = 1
        };
        var countWarehouse = new Warehouse
        {
            TenantId = tenantId,
            Code = "MAIN",
            Name = "Main warehouse",
            IsDefault = true
        };
        var secondaryWarehouse = new Warehouse
        {
            TenantId = tenantId,
            Code = "SIDE",
            Name = "Side warehouse",
            IsDefault = false
        };
        dbContext.Products.Add(product);
        dbContext.Warehouses.Add(countWarehouse);
        dbContext.Warehouses.Add(secondaryWarehouse);
        await dbContext.SaveChangesAsync();
        var count = await service.CreateAsync(new CreateInventoryCountRequest("Daily count", countWarehouse.Id), CancellationToken.None);

        await stockService.CreateMovementAsync(new CreateStockMovementRequest(product.Id, StockMovementType.In, 2, "other warehouse", secondaryWarehouse.Id), CancellationToken.None);
        var afterOtherWarehouseMovement = await service.GetAsync(count.Id, CancellationToken.None);

        Assert.False(afterOtherWarehouseMovement.HasPostSnapshotMovements);
        Assert.Equal(0, afterOtherWarehouseMovement.PostSnapshotMovementCount);
        Assert.Null(afterOtherWarehouseMovement.LastPostSnapshotMovementAt);

        var postSnapshotProduct = new Product
        {
            TenantId = tenantId,
            Sku = "SKU-COUNT-WARN-2",
            Name = "Post snapshot product",
            CurrentStock = 0,
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(postSnapshotProduct);
        await dbContext.SaveChangesAsync();

        await stockService.CreateMovementAsync(new CreateStockMovementRequest(postSnapshotProduct.Id, StockMovementType.In, 1, "same warehouse", count.WarehouseId), CancellationToken.None);
        var afterCountWarehouseMovement = await service.GetAsync(count.Id, CancellationToken.None);

        Assert.True(afterCountWarehouseMovement.HasPostSnapshotMovements);
        Assert.Equal(1, afterCountWarehouseMovement.PostSnapshotMovementCount);
        Assert.NotNull(afterCountWarehouseMovement.LastPostSnapshotMovementAt);
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
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
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
