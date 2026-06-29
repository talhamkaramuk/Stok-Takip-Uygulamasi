using Microsoft.EntityFrameworkCore;
using STOKIO.Domain.Entities;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlConstraintTests(PostgreSqlDatabaseFixture fixture)
{
    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task Products_EnforceUniqueSku_PerTenant()
    {
        var tenantId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);
        await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        dbContext.Products.AddRange(
            NewProduct(tenantId, "DUP-SKU", "First Product"),
            NewProduct(tenantId, "DUP-SKU", "Second Product"));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProductBarcodes_EnforceUniqueBarcode_PerTenant()
    {
        var tenantId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);
        await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var first = NewProduct(tenantId, "SKU-1", "First Product");
        var second = NewProduct(tenantId, "SKU-2", "Second Product");
        dbContext.Products.AddRange(first, second);
        dbContext.ProductBarcodes.AddRange(
            new ProductBarcode { TenantId = tenantId, Product = first, Barcode = "8690000000011", IsPrimary = true },
            new ProductBarcode { TenantId = tenantId, Product = second, Barcode = "8690000000011", IsPrimary = true });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task Products_EnforceNonNegativeCurrentStock_CheckConstraint()
    {
        var tenantId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);
        await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        dbContext.Products.Add(NewProduct(tenantId, "NEG-STOCK", "Invalid Product", currentStock: -1));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static Product NewProduct(Guid tenantId, string sku, string name, int currentStock = 0)
    {
        return new Product
        {
            TenantId = tenantId,
            Sku = sku,
            Name = name,
            CurrentStock = currentStock,
            CriticalStockLevel = 1
        };
    }
}
