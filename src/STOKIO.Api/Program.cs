using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using STOKIO.Api.Configuration;
using STOKIO.Api.Endpoints;
using STOKIO.Api.Middleware;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Validation;
using STOKIO.Infrastructure;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Security;
using STOKIO.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterTenantRequestValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IIdempotencyKeyAccessor, HttpIdempotencyKeyAccessor>();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
var databaseStartupOptions = DatabaseStartupOptions.FromConfiguration(builder.Configuration);

if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantOwners", policy => policy.RequireRole("Owner"));
    options.AddPolicy("CatalogManagers", policy => policy.RequireRole("Owner", "Manager"));
    options.AddPolicy("AuthenticatedStaff", policy => policy.RequireRole("Owner", "Manager", "Staff"));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Cast<string>()
    .ToArray();

if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    allowedOrigins = ["http://localhost:5173"];
}

StartupSafety.Validate(builder.Environment, jwtOptions, allowedOrigins, databaseStartupOptions);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddStokioRateLimiting();

var app = builder.Build();

app.UseMiddleware<ProblemDetailsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (databaseStartupOptions.EnsureCreated
    || databaseStartupOptions.ApplyDevelopmentSchemaPatches
    || databaseStartupOptions.SeedDevelopmentData)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<StokioDbContext>();

    if (databaseStartupOptions.EnsureCreated)
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    if (databaseStartupOptions.ApplyDevelopmentSchemaPatches)
    {
        await ApplyDevelopmentSchemaPatchesAsync(dbContext);
    }

    if (databaseStartupOptions.SeedDevelopmentData)
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, passwordHasher);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "STOKIO.Api" }))
    .AllowAnonymous()
    .WithTags("Health");

app.MapGet("/health/ready", async (StokioDbContext dbContext, CancellationToken cancellationToken) =>
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return Results.Json(
                    new { status = "unavailable", service = "STOKIO.Api", database = "unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new { status = "ready", service = "STOKIO.Api", database = "available" });
        }
        catch
        {
            return Results.Json(
                new { status = "unavailable", service = "STOKIO.Api", database = "unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .AllowAnonymous()
    .WithTags("Health");

app.MapAuthEndpoints("/api/auth");
app.MapProductEndpoints("/api/products");
app.MapCategoryEndpoints("/api/categories");
app.MapCustomerEndpoints("/api/customers");
app.MapSupplierEndpoints("/api/suppliers");
app.MapWarehouseEndpoints("/api/warehouses");
app.MapUserEndpoints("/api/users");
app.MapStockEndpoints("/api/stock");
app.MapInventoryCountEndpoints("/api/counts");
app.MapSalesOrderEndpoints("/api/orders");
app.MapPurchaseRequestEndpoints("/api/purchase-requests");
app.MapShipmentEndpoints("/api/shipments");
app.MapReturnRequestEndpoints("/api/returns");
app.MapReportEndpoints("/api/reports");
app.MapExportEndpoints("/api/exports");

app.MapAuthEndpoints("/api/v1/auth");
app.MapProductEndpoints("/api/v1/products");
app.MapCategoryEndpoints("/api/v1/categories");
app.MapCustomerEndpoints("/api/v1/customers");
app.MapSupplierEndpoints("/api/v1/suppliers");
app.MapWarehouseEndpoints("/api/v1/warehouses");
app.MapUserEndpoints("/api/v1/users");
app.MapStockEndpoints("/api/v1/stock");
app.MapInventoryCountEndpoints("/api/v1/counts");
app.MapSalesOrderEndpoints("/api/v1/orders");
app.MapPurchaseRequestEndpoints("/api/v1/purchase-requests");
app.MapShipmentEndpoints("/api/v1/shipments");
app.MapReturnRequestEndpoints("/api/v1/returns");
app.MapReportEndpoints("/api/v1/reports");
app.MapExportEndpoints("/api/v1/exports");

await app.RunAsync();

static async Task ApplyDevelopmentSchemaPatchesAsync(StokioDbContext dbContext)
{
    if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
    {
        return;
    }

    await dbContext.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TaxNumber" character varying(50);
        ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Phone" character varying(30);
        ALTER TABLE "AuditLogs" ADD COLUMN IF NOT EXISTS "OldValueJson" jsonb;
        ALTER TABLE "AuditLogs" ADD COLUMN IF NOT EXISTS "NewValueJson" jsonb;
        ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
        ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "WarehouseId" uuid;
        ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "TransferGroupId" uuid;
        ALTER TABLE "InventoryCounts" ADD COLUMN IF NOT EXISTS "WarehouseId" uuid;

        CREATE TABLE IF NOT EXISTS "Warehouses" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "Code" character varying(32) NOT NULL,
            "Name" character varying(140) NOT NULL,
            "Address" character varying(300) NULL,
            "IsDefault" boolean NOT NULL DEFAULT false,
            "IsActive" boolean NOT NULL DEFAULT true,
            CONSTRAINT "PK_Warehouses" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Warehouses_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Warehouses_TenantId_Code" ON "Warehouses" ("TenantId", "Code");
        CREATE INDEX IF NOT EXISTS "IX_Warehouses_TenantId_Name" ON "Warehouses" ("TenantId", "Name");

        CREATE TABLE IF NOT EXISTS "WarehouseStocks" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "WarehouseId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "Quantity" integer NOT NULL,
            "Version" integer NOT NULL DEFAULT 1,
            CONSTRAINT "PK_WarehouseStocks" PRIMARY KEY ("Id"),
            CONSTRAINT "CK_WarehouseStocks_Quantity_NonNegative" CHECK ("Quantity" >= 0),
            CONSTRAINT "FK_WarehouseStocks_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_WarehouseStocks_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_WarehouseStocks_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
        );

        ALTER TABLE "WarehouseStocks" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WarehouseStocks_TenantId_WarehouseId_ProductId" ON "WarehouseStocks" ("TenantId", "WarehouseId", "ProductId");
        CREATE INDEX IF NOT EXISTS "IX_WarehouseStocks_TenantId_ProductId" ON "WarehouseStocks" ("TenantId", "ProductId");
        CREATE INDEX IF NOT EXISTS "IX_StockMovements_TenantId_WarehouseId" ON "StockMovements" ("TenantId", "WarehouseId");
        CREATE INDEX IF NOT EXISTS "IX_StockMovements_TenantId_TransferGroupId" ON "StockMovements" ("TenantId", "TransferGroupId");

        CREATE TABLE IF NOT EXISTS "IdempotencyRecords" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "Key" character varying(160) NOT NULL,
            "OperationName" character varying(160) NOT NULL,
            "RequestHash" character varying(88) NOT NULL,
            "Status" character varying(32) NOT NULL DEFAULT 'Completed',
            "ResourceType" character varying(80) NOT NULL,
            "ResourceId" character varying(80) NOT NULL,
            "ResponseSnapshotJson" jsonb NULL,
            "CompletedAt" timestamp with time zone NULL,
            "ExpiresAt" timestamp with time zone NOT NULL DEFAULT (now() + interval '24 hours'),
            CONSTRAINT "PK_IdempotencyRecords" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_IdempotencyRecords_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
        );

        ALTER TABLE "IdempotencyRecords" ADD COLUMN IF NOT EXISTS "Status" character varying(32) NOT NULL DEFAULT 'Completed';
        ALTER TABLE "IdempotencyRecords" ADD COLUMN IF NOT EXISTS "ResponseSnapshotJson" jsonb;
        ALTER TABLE "IdempotencyRecords" ADD COLUMN IF NOT EXISTS "CompletedAt" timestamp with time zone;
        ALTER TABLE "IdempotencyRecords" ALTER COLUMN "CompletedAt" DROP NOT NULL;
        ALTER TABLE "IdempotencyRecords" ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NOT NULL DEFAULT (now() + interval '24 hours');
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_IdempotencyRecords_TenantId_OperationName_Key" ON "IdempotencyRecords" ("TenantId", "OperationName", "Key");

        CREATE TABLE IF NOT EXISTS "Customers" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "Code" character varying(32) NOT NULL,
            "Name" character varying(180) NOT NULL,
            "ContactName" character varying(120) NULL,
            "Email" character varying(180) NULL,
            "Phone" character varying(40) NULL,
            "TaxNumber" character varying(50) NULL,
            "Address" character varying(300) NULL,
            "Notes" character varying(500) NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Customers_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS "Suppliers" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "Code" character varying(32) NOT NULL,
            "Name" character varying(180) NOT NULL,
            "ContactName" character varying(120) NULL,
            "Email" character varying(180) NULL,
            "Phone" character varying(40) NULL,
            "TaxNumber" character varying(50) NULL,
            "Address" character varying(300) NULL,
            "Notes" character varying(500) NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Suppliers_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_TenantId_Code" ON "Customers" ("TenantId", "Code");
        CREATE INDEX IF NOT EXISTS "IX_Customers_TenantId_Name" ON "Customers" ("TenantId", "Name");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Suppliers_TenantId_Code" ON "Suppliers" ("TenantId", "Code");
        CREATE INDEX IF NOT EXISTS "IX_Suppliers_TenantId_Name" ON "Suppliers" ("TenantId", "Name");

        CREATE TABLE IF NOT EXISTS "SalesOrders" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "OrderNumber" character varying(40) NOT NULL,
            "CustomerId" uuid NULL,
            "CustomerName" character varying(180) NOT NULL,
            "WarehouseId" uuid NULL,
            "Status" character varying(32) NOT NULL,
            "Notes" character varying(500) NULL,
            "CreatedByUserId" uuid NULL,
            CONSTRAINT "PK_SalesOrders" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SalesOrders_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_SalesOrders_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_SalesOrders_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_SalesOrders_ApplicationUsers_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "ApplicationUsers" ("Id") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS "SalesOrderItems" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "SalesOrderId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "Quantity" integer NOT NULL,
            "ShippedQuantity" integer NOT NULL DEFAULT 0,
            "ReturnedQuantity" integer NOT NULL DEFAULT 0,
            "Version" integer NOT NULL DEFAULT 1,
            CONSTRAINT "PK_SalesOrderItems" PRIMARY KEY ("Id"),
            CONSTRAINT "CK_SalesOrderItems_Quantity_Positive" CHECK ("Quantity" > 0),
            CONSTRAINT "CK_SalesOrderItems_ShippedQuantity_NonNegative" CHECK ("ShippedQuantity" >= 0),
            CONSTRAINT "CK_SalesOrderItems_ReturnedQuantity_NonNegative" CHECK ("ReturnedQuantity" >= 0),
            CONSTRAINT "CK_SalesOrderItems_ShippedQuantity_NotOverOrdered" CHECK ("ShippedQuantity" <= "Quantity"),
            CONSTRAINT "CK_SalesOrderItems_ReturnedQuantity_NotOverShipped" CHECK ("ReturnedQuantity" <= "ShippedQuantity"),
            CONSTRAINT "FK_SalesOrderItems_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_SalesOrderItems_SalesOrders_SalesOrderId" FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SalesOrderItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS "PurchaseRequests" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "RequestNumber" character varying(40) NOT NULL,
            "SupplierId" uuid NULL,
            "SupplierName" character varying(180) NOT NULL,
            "WarehouseId" uuid NULL,
            "Status" character varying(32) NOT NULL,
            "Notes" character varying(500) NULL,
            "RequestedByUserId" uuid NULL,
            "ApprovedAt" timestamp with time zone NULL,
            "ReceivedAt" timestamp with time zone NULL,
            CONSTRAINT "PK_PurchaseRequests" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_PurchaseRequests_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_PurchaseRequests_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_PurchaseRequests_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_PurchaseRequests_ApplicationUsers_RequestedByUserId" FOREIGN KEY ("RequestedByUserId") REFERENCES "ApplicationUsers" ("Id") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS "PurchaseRequestItems" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "PurchaseRequestId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "Quantity" integer NOT NULL,
            "ReceivedQuantity" integer NOT NULL DEFAULT 0,
            "Version" integer NOT NULL DEFAULT 1,
            CONSTRAINT "PK_PurchaseRequestItems" PRIMARY KEY ("Id"),
            CONSTRAINT "CK_PurchaseRequestItems_Quantity_Positive" CHECK ("Quantity" > 0),
            CONSTRAINT "CK_PurchaseRequestItems_ReceivedQuantity_NonNegative" CHECK ("ReceivedQuantity" >= 0),
            CONSTRAINT "CK_PurchaseRequestItems_ReceivedQuantity_NotOverRequested" CHECK ("ReceivedQuantity" <= "Quantity"),
            CONSTRAINT "FK_PurchaseRequestItems_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_PurchaseRequestItems_PurchaseRequests_PurchaseRequestId" FOREIGN KEY ("PurchaseRequestId") REFERENCES "PurchaseRequests" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_PurchaseRequestItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS "Shipments" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "ShipmentNumber" character varying(40) NOT NULL,
            "SalesOrderId" uuid NULL,
            "CustomerId" uuid NULL,
            "WarehouseId" uuid NULL,
            "RecipientName" character varying(180) NOT NULL,
            "TrackingNumber" character varying(80) NULL,
            "Status" character varying(32) NOT NULL,
            "ShippedAt" timestamp with time zone NOT NULL,
            "Notes" character varying(500) NULL,
            CONSTRAINT "PK_Shipments" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Shipments_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_Shipments_SalesOrders_SalesOrderId" FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_Shipments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_Shipments_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS "ShipmentItems" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "ShipmentId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "Quantity" integer NOT NULL,
            CONSTRAINT "PK_ShipmentItems" PRIMARY KEY ("Id"),
            CONSTRAINT "CK_ShipmentItems_Quantity_Positive" CHECK ("Quantity" > 0),
            CONSTRAINT "FK_ShipmentItems_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_ShipmentItems_Shipments_ShipmentId" FOREIGN KEY ("ShipmentId") REFERENCES "Shipments" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ShipmentItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS "ReturnRequests" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "ReturnNumber" character varying(40) NOT NULL,
            "SalesOrderId" uuid NULL,
            "CustomerId" uuid NULL,
            "WarehouseId" uuid NULL,
            "CustomerName" character varying(180) NOT NULL,
            "Reason" character varying(500) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "ReceivedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_ReturnRequests" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_ReturnRequests_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_ReturnRequests_SalesOrders_SalesOrderId" FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ReturnRequests_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ReturnRequests_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS "ReturnRequestItems" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" uuid NOT NULL,
            "ReturnRequestId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "Quantity" integer NOT NULL,
            CONSTRAINT "PK_ReturnRequestItems" PRIMARY KEY ("Id"),
            CONSTRAINT "CK_ReturnRequestItems_Quantity_Positive" CHECK ("Quantity" > 0),
            CONSTRAINT "FK_ReturnRequestItems_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_ReturnRequestItems_ReturnRequests_ReturnRequestId" FOREIGN KEY ("ReturnRequestId") REFERENCES "ReturnRequests" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ReturnRequestItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
        );

        ALTER TABLE "SalesOrders" ADD COLUMN IF NOT EXISTS "CustomerId" uuid;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "SupplierId" uuid;
        ALTER TABLE "Shipments" ADD COLUMN IF NOT EXISTS "CustomerId" uuid;
        ALTER TABLE "ReturnRequests" ADD COLUMN IF NOT EXISTS "CustomerId" uuid;
        ALTER TABLE "SalesOrderItems" ADD COLUMN IF NOT EXISTS "ShippedQuantity" integer NOT NULL DEFAULT 0;
        ALTER TABLE "SalesOrderItems" ADD COLUMN IF NOT EXISTS "ReturnedQuantity" integer NOT NULL DEFAULT 0;
        ALTER TABLE "SalesOrderItems" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "ReceivedQuantity" integer NOT NULL DEFAULT 0;
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
        UPDATE "PurchaseRequestItems" AS pri
        SET "ReceivedQuantity" = pri."Quantity"
        FROM "PurchaseRequests" AS pr
        WHERE pri."PurchaseRequestId" = pr."Id"
          AND pr."Status" = 'Received'
          AND pri."ReceivedQuantity" = 0;
        UPDATE "SalesOrderItems" AS soi
        SET "ShippedQuantity" = soi."Quantity"
        FROM "SalesOrders" AS so
        WHERE soi."SalesOrderId" = so."Id"
          AND so."Status" IN ('Shipped', 'Completed')
          AND soi."ShippedQuantity" = 0;
        UPDATE "SalesOrders" SET "Status" = 'Pending' WHERE "Status" = 'Preparing';
        UPDATE "SalesOrders" SET "Status" = 'Shipped' WHERE "Status" = 'Completed';

        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalesOrders_Customers_CustomerId') THEN
                ALTER TABLE "SalesOrders" ADD CONSTRAINT "FK_SalesOrders_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PurchaseRequests_Suppliers_SupplierId') THEN
                ALTER TABLE "PurchaseRequests" ADD CONSTRAINT "FK_PurchaseRequests_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Shipments_Customers_CustomerId') THEN
                ALTER TABLE "Shipments" ADD CONSTRAINT "FK_Shipments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ReturnRequests_Customers_CustomerId') THEN
                ALTER TABLE "ReturnRequests" ADD CONSTRAINT "FK_ReturnRequests_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_SalesOrderItems_ShippedQuantity_NonNegative') THEN
                ALTER TABLE "SalesOrderItems" ADD CONSTRAINT "CK_SalesOrderItems_ShippedQuantity_NonNegative" CHECK ("ShippedQuantity" >= 0);
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_SalesOrderItems_ReturnedQuantity_NonNegative') THEN
                ALTER TABLE "SalesOrderItems" ADD CONSTRAINT "CK_SalesOrderItems_ReturnedQuantity_NonNegative" CHECK ("ReturnedQuantity" >= 0);
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_SalesOrderItems_ShippedQuantity_NotOverOrdered') THEN
                ALTER TABLE "SalesOrderItems" ADD CONSTRAINT "CK_SalesOrderItems_ShippedQuantity_NotOverOrdered" CHECK ("ShippedQuantity" <= "Quantity");
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_SalesOrderItems_ReturnedQuantity_NotOverShipped') THEN
                ALTER TABLE "SalesOrderItems" ADD CONSTRAINT "CK_SalesOrderItems_ReturnedQuantity_NotOverShipped" CHECK ("ReturnedQuantity" <= "ShippedQuantity");
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_PurchaseRequestItems_ReceivedQuantity_NonNegative') THEN
                ALTER TABLE "PurchaseRequestItems" ADD CONSTRAINT "CK_PurchaseRequestItems_ReceivedQuantity_NonNegative" CHECK ("ReceivedQuantity" >= 0);
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_PurchaseRequestItems_ReceivedQuantity_NotOverRequested') THEN
                ALTER TABLE "PurchaseRequestItems" ADD CONSTRAINT "CK_PurchaseRequestItems_ReceivedQuantity_NotOverRequested" CHECK ("ReceivedQuantity" <= "Quantity");
            END IF;
        END $$;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesOrders_TenantId_OrderNumber" ON "SalesOrders" ("TenantId", "OrderNumber");
        CREATE INDEX IF NOT EXISTS "IX_SalesOrders_TenantId_CustomerId" ON "SalesOrders" ("TenantId", "CustomerId");
        CREATE INDEX IF NOT EXISTS "IX_SalesOrders_TenantId_Status" ON "SalesOrders" ("TenantId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_SalesOrderItems_TenantId_SalesOrderId_ProductId" ON "SalesOrderItems" ("TenantId", "SalesOrderId", "ProductId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PurchaseRequests_TenantId_RequestNumber" ON "PurchaseRequests" ("TenantId", "RequestNumber");
        CREATE INDEX IF NOT EXISTS "IX_PurchaseRequests_TenantId_SupplierId" ON "PurchaseRequests" ("TenantId", "SupplierId");
        CREATE INDEX IF NOT EXISTS "IX_PurchaseRequests_TenantId_Status" ON "PurchaseRequests" ("TenantId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_PurchaseRequestItems_TenantId_PurchaseRequestId_ProductId" ON "PurchaseRequestItems" ("TenantId", "PurchaseRequestId", "ProductId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Shipments_TenantId_ShipmentNumber" ON "Shipments" ("TenantId", "ShipmentNumber");
        CREATE INDEX IF NOT EXISTS "IX_Shipments_TenantId_CustomerId" ON "Shipments" ("TenantId", "CustomerId");
        CREATE INDEX IF NOT EXISTS "IX_Shipments_TenantId_Status" ON "Shipments" ("TenantId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_ShipmentItems_TenantId_ShipmentId_ProductId" ON "ShipmentItems" ("TenantId", "ShipmentId", "ProductId");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReturnRequests_TenantId_ReturnNumber" ON "ReturnRequests" ("TenantId", "ReturnNumber");
        CREATE INDEX IF NOT EXISTS "IX_ReturnRequests_TenantId_CustomerId" ON "ReturnRequests" ("TenantId", "CustomerId");
        CREATE INDEX IF NOT EXISTS "IX_ReturnRequests_TenantId_Status" ON "ReturnRequests" ("TenantId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_ReturnRequestItems_TenantId_ReturnRequestId_ProductId" ON "ReturnRequestItems" ("TenantId", "ReturnRequestId", "ProductId");
        """);
}
