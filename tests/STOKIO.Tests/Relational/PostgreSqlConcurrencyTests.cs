using Microsoft.EntityFrameworkCore;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Services;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlConcurrencyTests(PostgreSqlDatabaseFixture fixture)
{
    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProductVersion_ThrowsConcurrencyException_WhenSameRowIsUpdatedTwice()
    {
        var tenantId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);

        await using (var seedContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId)))
        {
            seedContext.Products.Add(new Product
            {
                Id = productId,
                TenantId = tenantId,
                Sku = "CONCURRENCY-1",
                Name = "Concurrency Product",
                CurrentStock = 10,
                CriticalStockLevel = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        await using var secondContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var firstProduct = await firstContext.Products.SingleAsync(x => x.Id == productId);
        var secondProduct = await secondContext.Products.SingleAsync(x => x.Id == productId);

        firstProduct.CurrentStock = 11;
        await firstContext.SaveChangesAsync();
        secondProduct.CurrentStock = 12;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task GetOrCreateStockAsync_DoesNotThrow_WhenFirstStockRowIsCreatedConcurrently()
    {
        var tenantId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);

        await using (var seedContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId)))
        {
            seedContext.Warehouses.Add(new Warehouse
            {
                Id = warehouseId,
                TenantId = tenantId,
                Code = "MAIN",
                Name = "Main Warehouse",
                IsDefault = true
            });
            seedContext.Products.Add(new Product
            {
                Id = productId,
                TenantId = tenantId,
                Sku = "STOCK-RACE-1",
                Name = "Concurrent Stock Product",
                CurrentStock = 0,
                CriticalStockLevel = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        var firstProduct = await firstContext.Products.SingleAsync(x => x.Id == productId);
        var firstLedger = new WarehouseStockLedger(firstContext, new TestCurrentTenant(tenantId));
        var firstStock = await firstLedger.GetOrCreateStockAsync(firstProduct, warehouseId, CancellationToken.None);
        await firstLedger.LockForStockWriteAsync([firstProduct], [firstStock], CancellationToken.None);

        var secondAttempt = Task.Run(async () =>
        {
            await using var secondContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            await using var secondTransaction = await secondContext.Database.BeginTransactionAsync();
            var secondProduct = await secondContext.Products.SingleAsync(x => x.Id == productId);
            var secondLedger = new WarehouseStockLedger(secondContext, new TestCurrentTenant(tenantId));
            var secondStock = await secondLedger.GetOrCreateStockAsync(secondProduct, warehouseId, CancellationToken.None);
            await secondLedger.LockForStockWriteAsync([secondProduct], [secondStock], CancellationToken.None);
            await secondTransaction.CommitAsync();
        });

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await firstTransaction.CommitAsync();

        var completed = await Task.WhenAny(secondAttempt, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(secondAttempt, completed);
        await secondAttempt;

        await using var assertContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var storedStock = Assert.Single(await assertContext.WarehouseStocks
            .Where(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            .ToListAsync());
        Assert.Equal(0, storedStock.Quantity);
    }
}
