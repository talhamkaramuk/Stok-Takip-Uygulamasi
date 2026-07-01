using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using STOKIO.Application.Abstractions;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Tests.Relational;

public sealed class PostgreSqlMigrationTests
{
    [PostgreSqlFact]
    [Trait("Layer", "Migration")]
    public async Task Migrations_ApplyCleanlyOnEmptyDatabase()
    {
        var databaseName = CreateDatabaseName("empty");
        var connectionString = await CreateDatabaseAsync(databaseName);

        try
        {
            await using var dbContext = CreateDbContext(connectionString);
            await dbContext.Database.MigrateAsync();

            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            Assert.Empty(pendingMigrations);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [PostgreSqlFact]
    [Trait("Layer", "Migration")]
    public async Task Migrations_ApplyCleanlyOnSeededDatabase()
    {
        var databaseName = CreateDatabaseName("seeded");
        var connectionString = await CreateDatabaseAsync(databaseName);

        try
        {
            await using var dbContext = CreateDbContext(connectionString);
            var migrations = dbContext.Database.GetMigrations().ToArray();
            Assert.NotEmpty(migrations);

            if (migrations.Length > 1)
            {
                var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
                await migrator.MigrateAsync(migrations[^2]);
                await SeedOperationalDataForMigrationUpgradeAsync(dbContext);
            }

            await dbContext.Database.MigrateAsync();

            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            Assert.Empty(pendingMigrations);

            await AssertSearchTextBackfilledAsync(dbContext);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    private static StokioDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StokioDbContext(options, new EmptyCurrentTenant());
    }

    private static async Task<string> CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(GetAdminConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{databaseName}";""";
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(PostgreSqlTestSettings.ConnectionString)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(GetAdminConnectionString());
        await connection.OpenAsync();

        await using (var terminateCommand = connection.CreateCommand())
        {
            terminateCommand.CommandText = """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName
                  AND pid <> pg_backend_pid();
                """;
            terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}";""";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private static string GetAdminConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(PostgreSqlTestSettings.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        return builder.ConnectionString;
    }

    private static string CreateDatabaseName(string suffix)
    {
        return $"stokio_test_migration_{suffix}_{Guid.NewGuid():N}"[..48];
    }

    private static async Task SeedOperationalDataForMigrationUpgradeAsync(StokioDbContext dbContext)
    {
        var tenantId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var salesOrderId = Guid.CreateVersion7();
        var purchaseRequestId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var returnRequestId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Tenants" ("Id", "Name", "Slug", "TaxNumber", "Phone", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({tenantId}, 'Migration Seed Tenant', 'migration-seed', NULL, NULL, TRUE, {now}, NULL);

            INSERT INTO "Warehouses" ("Id", "TenantId", "Code", "Name", "Address", "IsDefault", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({warehouseId}, {tenantId}, 'MAIN', 'Main Warehouse', NULL, TRUE, TRUE, {now}, NULL);

            INSERT INTO "SalesOrders" ("Id", "TenantId", "OrderNumber", "CustomerId", "CustomerName", "WarehouseId", "Status", "Notes", "CreatedByUserId", "CreatedAt", "UpdatedAt")
            VALUES ({salesOrderId}, {tenantId}, 'SO-MIG-001', NULL, 'Migration Customer', {warehouseId}, 'Pending', 'seeded before latest migration', NULL, {now}, NULL);

            INSERT INTO "PurchaseRequests" ("Id", "TenantId", "RequestNumber", "SupplierId", "SupplierName", "WarehouseId", "Status", "Notes", "RequestedByUserId", "ApprovedAt", "ReceivedAt", "CreatedAt", "UpdatedAt")
            VALUES ({purchaseRequestId}, {tenantId}, 'PR-MIG-001', NULL, 'Migration Supplier', {warehouseId}, 'PendingApproval', 'seeded before latest migration', NULL, NULL, NULL, {now}, NULL);

            INSERT INTO "Shipments" ("Id", "TenantId", "ShipmentNumber", "SalesOrderId", "CustomerId", "WarehouseId", "RecipientName", "TrackingNumber", "Status", "ShippedAt", "Notes", "CreatedAt", "UpdatedAt")
            VALUES ({shipmentId}, {tenantId}, 'SHP-MIG-001', {salesOrderId}, NULL, {warehouseId}, 'Migration Customer', 'TRK-MIG-001', 'Completed', {now}, 'seeded before latest migration', {now}, NULL);

            INSERT INTO "ReturnRequests" ("Id", "TenantId", "ReturnNumber", "SalesOrderId", "CustomerId", "WarehouseId", "CustomerName", "Reason", "Status", "ReceivedAt", "CreatedAt", "UpdatedAt")
            VALUES ({returnRequestId}, {tenantId}, 'RET-MIG-001', {salesOrderId}, NULL, {warehouseId}, 'Migration Customer', 'Migration return reason', 'Received', {now}, {now}, NULL);
            """);
    }

    private static async Task AssertSearchTextBackfilledAsync(StokioDbContext dbContext)
    {
        await AssertBackfilledAsync(
            dbContext,
            "SalesOrders",
            $"""SELECT COUNT(*) AS "Value" FROM "SalesOrders" WHERE "SearchText" <> ''""");
        await AssertBackfilledAsync(
            dbContext,
            "PurchaseRequests",
            $"""SELECT COUNT(*) AS "Value" FROM "PurchaseRequests" WHERE "SearchText" <> ''""");
        await AssertBackfilledAsync(
            dbContext,
            "Shipments",
            $"""SELECT COUNT(*) AS "Value" FROM "Shipments" WHERE "SearchText" <> ''""");
        await AssertBackfilledAsync(
            dbContext,
            "ReturnRequests",
            $"""SELECT COUNT(*) AS "Value" FROM "ReturnRequests" WHERE "SearchText" <> ''""");
    }

    private static async Task AssertBackfilledAsync(StokioDbContext dbContext, string tableName, FormattableString query)
    {
        var count = await dbContext.Database.SqlQuery<int>(query).SingleAsync();
        Assert.True(count > 0, $"{tableName}.SearchText was not backfilled for seeded rows.");
    }

    private sealed class EmptyCurrentTenant : ICurrentTenant
    {
        public bool HasTenant => false;
        public Guid TenantId => Guid.Empty;
        public string? TenantSlug => null;

        public void SetTenant(Guid tenantId, string? slug)
        {
        }
    }
}
