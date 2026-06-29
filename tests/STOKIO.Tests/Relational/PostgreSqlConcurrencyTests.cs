using Microsoft.EntityFrameworkCore;
using STOKIO.Domain.Entities;
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
}
