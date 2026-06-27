using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Application.Dtos.Parties;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Parties;

public sealed class PartyServicesTests
{
    [Fact]
    public async Task Customer_And_Supplier_Cards_Link_To_Operations()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var customerService = new CustomerService(dbContext, tenant, auditWriter);
        var supplierService = new SupplierService(dbContext, tenant, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var purchaseService = new PurchaseRequestService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "PARTY-1",
            Name = "Party Product",
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var customer = await customerService.CreateAsync(new CreateCustomerRequest(
            " c-001 ",
            "ACME Market",
            "Ayse Kaya",
            "ayse@example.com",
            "5551112233",
            null,
            null,
            null), CancellationToken.None);
        var supplier = await supplierService.CreateAsync(new CreateSupplierRequest(
            " s-001 ",
            "Telco Tedarik",
            null,
            "tedarik@example.com",
            null,
            null,
            null,
            null), CancellationToken.None);

        var order = await orderService.CreateAsync(new CreateSalesOrderRequest(
            "Serbest Müşteri",
            null,
            null,
            [new OperationItemRequest(product.Id, 1)],
            customer.Id), CancellationToken.None);
        var purchase = await purchaseService.CreateAsync(new CreatePurchaseRequestRequest(
            "Serbest Tedarikçi",
            null,
            null,
            [new OperationItemRequest(product.Id, 2)],
            supplier.Id), CancellationToken.None);

        Assert.Equal("C-001", customer.Code);
        Assert.Equal("S-001", supplier.Code);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal("ACME Market", order.CustomerName);
        Assert.Equal(supplier.Id, purchase.SupplierId);
        Assert.Equal("Telco Tedarik", purchase.SupplierName);
    }

    [Fact]
    public async Task Operations_Reject_Inactive_Party_Cards()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var user = new TestCurrentUser();
        var clock = new TestClock();
        var auditWriter = new AuditWriter(dbContext, tenant, user);
        var ledger = new WarehouseStockLedger(dbContext, tenant);
        var customerService = new CustomerService(dbContext, tenant, auditWriter);
        var orderService = new SalesOrderService(dbContext, tenant, user, clock, ledger, auditWriter);
        var product = new Product
        {
            TenantId = tenantId,
            Sku = "PARTY-2",
            Name = "Inactive Party Product",
            CriticalStockLevel = 1
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var customer = await customerService.CreateAsync(new CreateCustomerRequest("C-002", "Pasif Musteri", null, null, null, null, null, null), CancellationToken.None);
        await customerService.DeactivateAsync(customer.Id, CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppProblemException>(() => orderService.CreateAsync(new CreateSalesOrderRequest(
            "Pasif Musteri",
            null,
            null,
            [new OperationItemRequest(product.Id, 1)],
            customer.Id), CancellationToken.None));
        Assert.Equal("customer_not_found", error.Code);
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
}
