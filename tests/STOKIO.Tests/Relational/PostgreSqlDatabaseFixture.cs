using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private const string TruncateSql = """
        TRUNCATE TABLE
            "AuditLogs",
            "ExportJobs",
            "IdempotencyRecords",
            "InventoryCountItems",
            "InventoryCounts",
            "ReturnRequestItems",
            "ReturnRequests",
            "ShipmentItems",
            "Shipments",
            "PurchaseRequestItems",
            "PurchaseRequests",
            "SalesOrderItems",
            "SalesOrders",
            "StockMovements",
            "WarehouseStocks",
            "Warehouses",
            "ProductBarcodes",
            "Products",
            "Customers",
            "Suppliers",
            "Categories",
            "ApplicationUsers",
            "Tenants"
        RESTART IDENTITY CASCADE;
        """;

    public async Task InitializeAsync()
    {
        if (!PostgreSqlTestSettings.IsEnabled)
        {
            return;
        }

        PostgreSqlTestSettings.EnsureSafeForDestructiveReset();
        await using var dbContext = CreateDbContext(new TestCurrentTenant(Guid.CreateVersion7()));
        await dbContext.Database.MigrateAsync();
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        if (PostgreSqlTestSettings.IsEnabled)
        {
            await ResetAsync();
        }
    }

    public StokioDbContext CreateDbContext(ICurrentTenant currentTenant)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseNpgsql(PostgreSqlTestSettings.ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        return new StokioDbContext(options, currentTenant);
    }

    public async Task ResetAsync()
    {
        PostgreSqlTestSettings.EnsureSafeForDestructiveReset();
        await using var dbContext = CreateDbContext(new TestCurrentTenant(Guid.CreateVersion7()));
        await dbContext.Database.ExecuteSqlRawAsync(TruncateSql);
    }

    public async Task<Tenant> SeedTenantAsync(Guid tenantId, string slug = "tenant-test")
    {
        await using var dbContext = CreateDbContext(new TestCurrentTenant(tenantId, slug));
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {slug}",
            Slug = slug
        };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
    }
}
