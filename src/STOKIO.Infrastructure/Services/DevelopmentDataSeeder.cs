using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(StokioDbContext dbContext, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        const string tenantSlug = "stokio-demo";
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Stokio Demo A.Ş.",
                Slug = tenantSlug,
                TaxNumber = "1234567890",
                Phone = "0212 555 10 20"
            };
            dbContext.Tenants.Add(tenant);
        }

        var hasOwner = await dbContext.ApplicationUsers
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Email == "owner@stokio.local", cancellationToken);
        if (!hasOwner)
        {
            dbContext.ApplicationUsers.Add(new ApplicationUser
            {
                TenantId = tenant.Id,
                Tenant = tenant,
                FullName = "Ahmet Yılmaz",
                Email = "owner@stokio.local",
                PasswordHash = passwordHasher.Hash("StrongPass123"),
                Role = UserRole.Owner
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var warehouses = await EnsureWarehousesAsync(dbContext, tenant.Id, cancellationToken);
        var categories = await EnsureCategoriesAsync(dbContext, tenant.Id, cancellationToken);
        var customers = await EnsureCustomersAsync(dbContext, tenant.Id, cancellationToken);
        var suppliers = await EnsureSuppliersAsync(dbContext, tenant.Id, cancellationToken);
        var products = await EnsureProductsAsync(dbContext, tenant.Id, categories, warehouses, cancellationToken);
        await EnsureOperationsAsync(dbContext, tenant.Id, products, warehouses, customers, suppliers, cancellationToken);
    }

    private static async Task<Dictionary<string, Warehouse>> EnsureWarehousesAsync(StokioDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new WarehouseSeed("MAIN", "Ana Depo", "İstanbul Merkez", true),
            new WarehouseSeed("ANK", "Ankara Depo", "Ankara Çankaya", false),
            new WarehouseSeed("IZM", "İzmir Depo", "İzmir Alsancak", false),
            new WarehouseSeed("BUR", "Bursa Depo", "Bursa Nilüfer", false)
        };

        foreach (var seed in seeds)
        {
            var exists = await dbContext.Warehouses
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Code == seed.Code, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Warehouses.Add(new Warehouse
            {
                TenantId = tenantId,
                Code = seed.Code,
                Name = seed.Name,
                Address = seed.Address,
                IsDefault = seed.IsDefault
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Warehouses
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, cancellationToken);
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(StokioDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        foreach (var name in new[] { "Elektronik", "Telefon Aksesuarı", "Bilgisayar Malzemesi", "Kırtasiye", "Kozmetik" })
        {
            var exists = await dbContext.Categories
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);
            if (!exists)
            {
                dbContext.Categories.Add(new Category { TenantId = tenantId, Name = name });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Categories
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Name, cancellationToken);
    }

    private static async Task<List<Customer>> EnsureCustomersAsync(StokioDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new PartySeed("C-001", "Techno Market A.Ş.", "Ahmet Yılmaz", "ahmet@technomarket.local", "0555 100 10 10", "İstanbul"),
            new PartySeed("C-002", "E-Satış Mağazası", "Zeynep Kaya", "zeynep@esatis.local", "0555 200 20 20", "Ankara"),
            new PartySeed("C-003", "Net Bilgisayar", "Mehmet Demir", "mehmet@netbilgisayar.local", "0555 300 30 30", "İzmir"),
            new PartySeed("C-004", "Ofis Line", "Ayşe Yıldız", "ayse@ofisline.local", "0555 400 40 40", "Bursa"),
            new PartySeed("C-005", "MobilExpress", "Can Polat", "can@mobilexpress.local", "0555 500 50 50", "İstanbul")
        };

        foreach (var seed in seeds)
        {
            var exists = await dbContext.Customers
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Code == seed.Code, cancellationToken);
            if (!exists)
            {
                dbContext.Customers.Add(new Customer
                {
                    TenantId = tenantId,
                    Code = seed.Code,
                    Name = seed.Name,
                    ContactName = seed.ContactName,
                    Email = seed.Email,
                    Phone = seed.Phone,
                    Address = seed.Address
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Customers
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Supplier>> EnsureSuppliersAsync(StokioDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new PartySeed("S-001", "Telco Tedarik A.Ş.", "Selin Koç", "selin@telco.local", "0555 610 10 10", "İstanbul"),
            new PartySeed("S-002", "Global Elektronik", "Mert Şahin", "mert@global.local", "0555 620 20 20", "Ankara"),
            new PartySeed("S-003", "Bilişim Market", "Ece Arslan", "ece@bilisimm.local", "0555 630 30 30", "İzmir"),
            new PartySeed("S-004", "Kırtasiye Plus", "Onur Acar", "onur@kirtasiyeplus.local", "0555 640 40 40", "Bursa")
        };

        foreach (var seed in seeds)
        {
            var exists = await dbContext.Suppliers
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Code == seed.Code, cancellationToken);
            if (!exists)
            {
                dbContext.Suppliers.Add(new Supplier
                {
                    TenantId = tenantId,
                    Code = seed.Code,
                    Name = seed.Name,
                    ContactName = seed.ContactName,
                    Email = seed.Email,
                    Phone = seed.Phone,
                    Address = seed.Address
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Suppliers
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Product>> EnsureProductsAsync(
        StokioDbContext dbContext,
        Guid tenantId,
        IReadOnlyDictionary<string, Category> categories,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new ProductSeed("KLK-X1-BLK", "Kablosuz Kulaklık X1", "Elektronik", 3, "8691234567890", 85),
            new ProductSeed("SAAT-PRO-SLV", "Akıllı Saat Pro", "Elektronik", 2, "8691234567891", 42),
            new ProductSeed("KAB-UC1-BLK", "USB-C Kablo 1m", "Telefon Aksesuarı", 6, "8691234567892", 160),
            new ProductSeed("SARJ-10000-BLK", "Taşınabilir Şarj Cihazı", "Telefon Aksesuarı", 4, "8691234567893", 58),
            new ProductSeed("KLAV-K3-RGB", "Klavye Mekanik K3", "Bilgisayar Malzemesi", 1, "8691234567894", 28),
            new ProductSeed("MOUSE-M3-BLK", "Mouse Kablosuz M3", "Bilgisayar Malzemesi", 5, "8691234567895", 72),
            new ProductSeed("DEF-A5-80", "A5 Çizgili Defter", "Kırtasiye", 20, "8691234567896", 240),
            new ProductSeed("KRM-HND-50", "El Kremi 50ml", "Kozmetik", 12, "8691234567897", 96)
        };

        foreach (var seed in seeds)
        {
            var skuExists = await dbContext.Products
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Sku == seed.Sku, cancellationToken);
            var barcodeExists = await dbContext.ProductBarcodes
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Barcode == seed.Barcode, cancellationToken);
            if (skuExists || barcodeExists)
            {
                continue;
            }

            var product = new Product
            {
                TenantId = tenantId,
                CategoryId = categories[seed.CategoryName].Id,
                Sku = seed.Sku,
                Name = seed.Name,
                CriticalStockLevel = seed.CriticalLevel,
                CurrentStock = seed.Stock
            };
            product.Barcodes.Add(new ProductBarcode
            {
                TenantId = tenantId,
                Product = product,
                Barcode = seed.Barcode,
                IsPrimary = true
            });
            dbContext.Products.Add(product);

            var main = warehouses["MAIN"];
            var ankara = warehouses["ANK"];
            var mainQty = (int)Math.Ceiling(seed.Stock * 0.65);
            var ankaraQty = seed.Stock - mainQty;
            dbContext.WarehouseStocks.AddRange(
                new WarehouseStock { TenantId = tenantId, Product = product, WarehouseId = main.Id, Quantity = mainQty },
                new WarehouseStock { TenantId = tenantId, Product = product, WarehouseId = ankara.Id, Quantity = ankaraQty });
            dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                Product = product,
                WarehouseId = main.Id,
                Type = StockMovementType.In,
                Quantity = seed.Stock,
                PreviousQuantity = 0,
                NewQuantity = seed.Stock,
                Reason = "Demo başlangıç stoğu"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Products
            .IgnoreQueryFilters()
            .Include(x => x.Barcodes)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Sku)
            .ToListAsync(cancellationToken);
    }

    private static async Task EnsureOperationsAsync(
        StokioDbContext dbContext,
        Guid tenantId,
        IReadOnlyList<Product> products,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Supplier> suppliers,
        CancellationToken cancellationToken)
    {
        var hasOperations = await dbContext.SalesOrders
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.OrderNumber.StartsWith("SO-DEMO-"), cancellationToken);
        if (hasOperations || products.Count < 4 || customers.Count == 0 || suppliers.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var orders = new List<SalesOrder>();
        for (var i = 0; i < 8; i++)
        {
            var product = products[i % products.Count];
            var customer = customers[i % customers.Count];
            var quantity = 3 + i % 3;
            var shippedQuantity = i < 3 ? 0 : i < 5 ? quantity - 1 : quantity;
            var returnedQuantity = i >= 4 ? 1 : 0;
            var order = new SalesOrder
            {
                TenantId = tenantId,
                OrderNumber = $"SO-DEMO-{i + 1:000}",
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                WarehouseId = warehouses[i % 2 == 0 ? "MAIN" : "ANK"].Id,
                Status = shippedQuantity == 0
                    ? SalesOrderStatus.Pending
                    : shippedQuantity < quantity
                        ? SalesOrderStatus.PartiallyShipped
                        : SalesOrderStatus.Shipped,
                Notes = "Demo sipariş"
            };
            order.Items.Add(new SalesOrderItem
            {
                TenantId = tenantId,
                ProductId = product.Id,
                Quantity = quantity,
                ShippedQuantity = shippedQuantity,
                ReturnedQuantity = returnedQuantity
            });
            orders.Add(order);
        }

        var purchases = new List<PurchaseRequest>();
        for (var i = 0; i < 6; i++)
        {
            var product = products[(i + 2) % products.Count];
            var supplier = suppliers[i % suppliers.Count];
            var request = new PurchaseRequest
            {
                TenantId = tenantId,
                RequestNumber = $"PR-DEMO-{i + 1:000}",
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                WarehouseId = warehouses["MAIN"].Id,
                Status = i < 2 ? PurchaseRequestStatus.PendingApproval : i < 4 ? PurchaseRequestStatus.Approved : PurchaseRequestStatus.Received,
                Notes = "Demo alım talebi",
                ApprovedAt = i >= 2 ? now.AddDays(-i) : null,
                ReceivedAt = i >= 4 ? now.AddDays(-i + 1) : null
            };
            request.Items.Add(new PurchaseRequestItem { TenantId = tenantId, ProductId = product.Id, Quantity = 4 + i });
            purchases.Add(request);
        }

        var shipments = new List<Shipment>();
        for (var i = 0; i < 5; i++)
        {
            var order = orders[i + 3];
            var orderItem = order.Items[0];
            var shipment = new Shipment
            {
                TenantId = tenantId,
                ShipmentNumber = $"SHP-DEMO-{i + 1:000}",
                SalesOrder = order,
                SalesOrderId = order.Id,
                CustomerId = order.CustomerId,
                RecipientName = order.CustomerName,
                WarehouseId = order.WarehouseId,
                TrackingNumber = $"TRK-DEMO-{i + 1:000}",
                Status = ShipmentStatus.Completed,
                ShippedAt = now.AddDays(-i)
            };
            shipment.Items.Add(new ShipmentItem { TenantId = tenantId, ProductId = orderItem.ProductId, Quantity = orderItem.ShippedQuantity });
            shipments.Add(shipment);
        }

        var returns = new List<ReturnRequest>();
        for (var i = 0; i < 4; i++)
        {
            var order = orders[i + 4];
            var orderItem = order.Items[0];
            var returnRequest = new ReturnRequest
            {
                TenantId = tenantId,
                ReturnNumber = $"RET-DEMO-{i + 1:000}",
                SalesOrder = order,
                SalesOrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                WarehouseId = order.WarehouseId,
                Reason = "Demo iade",
                Status = ReturnRequestStatus.Received,
                ReceivedAt = now.AddDays(-i)
            };
            returnRequest.Items.Add(new ReturnRequestItem { TenantId = tenantId, ProductId = orderItem.ProductId, Quantity = orderItem.ReturnedQuantity });
            returns.Add(returnRequest);
        }

        dbContext.SalesOrders.AddRange(orders);
        dbContext.PurchaseRequests.AddRange(purchases);
        dbContext.Shipments.AddRange(shipments);
        dbContext.ReturnRequests.AddRange(returns);
        await dbContext.SaveChangesAsync(cancellationToken);

        SetCreatedAt(orders, now, 8);
        SetCreatedAt(purchases, now, 6);
        SetCreatedAt(shipments, now, 5);
        SetCreatedAt(returns, now, 4);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void SetCreatedAt<T>(IReadOnlyList<T> entities, DateTimeOffset now, int spreadDays)
        where T : Entity
    {
        for (var i = 0; i < entities.Count; i++)
        {
            entities[i].CreatedAt = now.AddDays(-(spreadDays - i));
        }
    }

    private sealed record WarehouseSeed(string Code, string Name, string Address, bool IsDefault);
    private sealed record PartySeed(string Code, string Name, string ContactName, string Email, string Phone, string Address);
    private sealed record ProductSeed(string Sku, string Name, string CategoryName, int CriticalLevel, string Barcode, int Stock);
}
