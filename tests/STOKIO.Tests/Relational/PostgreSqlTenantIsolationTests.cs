using Microsoft.EntityFrameworkCore;
using STOKIO.Domain.Entities;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlTenantIsolationTests(PostgreSqlDatabaseFixture fixture)
{
    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task QueryFilters_ReturnOnlyCurrentTenantRows()
    {
        var firstTenantId = Guid.CreateVersion7();
        var secondTenantId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(firstTenantId, "tenant-a");
        await fixture.SeedTenantAsync(secondTenantId, "tenant-b");

        await using (var dbContext = fixture.CreateDbContext(new TestCurrentTenant(firstTenantId, "tenant-a")))
        {
            dbContext.Products.Add(new Product
            {
                TenantId = firstTenantId,
                Sku = "TENANT-A-SKU",
                Name = "Tenant A Product",
                CurrentStock = 1,
                CriticalStockLevel = 1
            });
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = fixture.CreateDbContext(new TestCurrentTenant(secondTenantId, "tenant-b")))
        {
            dbContext.Products.Add(new Product
            {
                TenantId = secondTenantId,
                Sku = "TENANT-B-SKU",
                Name = "Tenant B Product",
                CurrentStock = 2,
                CriticalStockLevel = 1
            });
            await dbContext.SaveChangesAsync();
        }

        await using var firstTenantContext = fixture.CreateDbContext(new TestCurrentTenant(firstTenantId, "tenant-a"));
        var visibleSkus = await firstTenantContext.Products.Select(x => x.Sku).ToListAsync();

        var sku = Assert.Single(visibleSkus);
        Assert.Equal("TENANT-A-SKU", sku);
    }
}
