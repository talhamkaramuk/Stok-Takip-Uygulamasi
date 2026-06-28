using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;

namespace STOKIO.Infrastructure.Persistence;

public sealed class StokioDbContext(DbContextOptions<StokioDbContext> options, ICurrentTenant currentTenant)
    : DbContext(options)
{
    private Guid TenantFilterId => currentTenant.HasTenant ? currentTenant.TenantId : Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountItem> InventoryCountItems => Set<InventoryCountItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnRequestItem> ReturnRequestItems => Set<ReturnRequestItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyEntityPolicies();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyEntityPolicies();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TaxNumber).HasMaxLength(50);
            entity.Property(x => x.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(180).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.TaxNumber).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.TaxNumber).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_products_current_stock_non_negative", "\"CurrentStock\" >= 0");
                table.HasCheckConstraint("ck_products_critical_stock_non_negative", "\"CriticalStockLevel\" >= 0");
            });
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductBarcode>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Barcode }).IsUnique();
            entity.Property(x => x.Barcode).HasMaxLength(128).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany(x => x.Barcodes).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseStock>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_warehouse_stocks_quantity_non_negative", "\"Quantity\" >= 0");
            });
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.WarehouseId, x.ProductId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProductId });
            entity.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany(x => x.Stocks).HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany(x => x.WarehouseStocks).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_stock_movements_quantity_non_negative", "\"Quantity\" >= 0");
                table.HasCheckConstraint("ck_stock_movements_new_quantity_non_negative", "\"NewQuantity\" >= 0");
            });
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.ProductId });
            entity.HasIndex(x => new { x.TenantId, x.WarehouseId });
            entity.HasIndex(x => new { x.TenantId, x.TransferGroupId });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InventoryCount>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InventoryCountItem>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_inventory_count_items_expected_non_negative", "\"ExpectedQuantity\" >= 0");
                table.HasCheckConstraint("ck_inventory_count_items_counted_non_negative", "\"CountedQuantity\" >= 0");
            });
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.InventoryCountId, x.ProductId }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InventoryCount).WithMany(x => x.Items).HasForeignKey(x => x.InventoryCountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany(x => x.SalesOrders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SalesOrderItem>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("ck_sales_order_items_quantity_positive", "\"Quantity\" > 0"));
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.SalesOrderId, x.ProductId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SalesOrder).WithMany(x => x.Items).HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseRequest>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.RequestNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier).WithMany(x => x.PurchaseRequests).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseRequestItem>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("ck_purchase_request_items_quantity_positive", "\"Quantity\" > 0"));
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.PurchaseRequestId, x.ProductId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PurchaseRequest).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.ShipmentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.ShipmentNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RecipientName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.TrackingNumber).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Customer).WithMany(x => x.Shipments).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("ck_shipment_items_quantity_positive", "\"Quantity\" > 0"));
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.ShipmentId, x.ProductId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shipment).WithMany(x => x.Items).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnRequest>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.ReturnNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Customer).WithMany(x => x.ReturnRequests).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReturnRequestItem>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("ck_return_request_items_quantity_positive", "\"Quantity\" > 0"));
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.ReturnRequestId, x.ProductId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReturnRequest).WithMany(x => x.Items).HasForeignKey(x => x.ReturnRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OldValueJson).HasColumnType("jsonb");
            entity.Property(x => x.NewValueJson).HasColumnType("jsonb");
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == TenantFilterId);
            entity.HasIndex(x => new { x.TenantId, x.OperationName, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.Property(x => x.OperationName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.RequestHash).HasMaxLength(88).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.ResourceType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ResourceId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ResponseSnapshotJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ApplyEntityPolicies()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }

        foreach (var entry in ChangeTracker.Entries<WarehouseStock>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity.TenantId == Guid.Empty)
            {
                if (!currentTenant.HasTenant)
                {
                    throw new InvalidOperationException("Tenant-scoped data cannot be saved without a tenant context.");
                }

                entry.Entity.TenantId = currentTenant.TenantId;
            }

            if (currentTenant.HasTenant && entry.Entity.TenantId != currentTenant.TenantId)
            {
                throw new UnauthorizedAccessException("Cross-tenant data modification was blocked.");
            }
        }
    }
}
