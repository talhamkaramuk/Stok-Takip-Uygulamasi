using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Products;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class ProductService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    WarehouseStockLedger stockLedger,
    AuditWriter auditWriter) : IProductService
{
    public async Task<PagedResult<ProductDto>> ListAsync(
        string? search,
        Guid? categoryId,
        bool? isActive,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Barcodes)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }
        else
        {
            query = query.Where(x => x.IsActive);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Sku.ToLower().Contains(term) ||
                x.Barcodes.Any(b => b.Barcode.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(
            products.Select(ToDto).ToList(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var product = await FindProductAsync(id, cancellationToken);
        return ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var sku = request.Sku.Trim();
        await EnsureSkuAvailableAsync(sku, null, cancellationToken);

        var barcodeValues = NormalizeBarcodes(request.Barcodes);
        await EnsureBarcodesAvailableAsync(barcodeValues, cancellationToken);

        var category = await FindOrCreateCategoryAsync(request.CategoryName, cancellationToken);
        var product = new Product
        {
            TenantId = currentTenant.TenantId,
            Category = category,
            Sku = sku,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CriticalStockLevel = request.CriticalStockLevel,
            CurrentStock = request.InitialStock
        };

        foreach (var barcode in barcodeValues)
        {
            product.Barcodes.Add(new ProductBarcode
            {
                TenantId = currentTenant.TenantId,
                Product = product,
                Barcode = barcode,
                IsPrimary = product.Barcodes.Count == 0
            });
        }

        dbContext.Products.Add(product);

        var defaultWarehouse = await stockLedger.GetDefaultWarehouseAsync(cancellationToken);
        product.WarehouseStocks.Add(new WarehouseStock
        {
            TenantId = currentTenant.TenantId,
            Warehouse = defaultWarehouse,
            Product = product,
            Quantity = request.InitialStock
        });

        if (request.InitialStock > 0)
        {
            dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = currentTenant.TenantId,
                Product = product,
                Warehouse = defaultWarehouse,
                Type = StockMovementType.In,
                Quantity = request.InitialStock,
                PreviousQuantity = 0,
                NewQuantity = request.InitialStock,
                Reason = "Initial stock",
                PerformedByUserId = currentUser.UserId
            });
        }

        auditWriter.AddSnapshot("product.created", nameof(Product), product.Id, null, ProductSnapshot(product));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var product = await FindProductAsync(id, cancellationToken);
        var oldValue = ProductSnapshot(product);
        var sku = request.Sku.Trim();
        await EnsureSkuAvailableAsync(sku, product.Id, cancellationToken);

        product.Category = await FindOrCreateCategoryAsync(request.CategoryName, cancellationToken);
        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.CriticalStockLevel = request.CriticalStockLevel;
        product.IsActive = request.IsActive;

        auditWriter.AddSnapshot("product.updated", nameof(Product), product.Id, oldValue, ProductSnapshot(product));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(product);
    }

    public async Task<ProductDto> AddBarcodeAsync(Guid productId, AddBarcodeRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var product = await FindProductAsync(productId, cancellationToken);
        var oldValue = ProductSnapshot(product);
        var barcode = request.Barcode.Trim();
        await EnsureBarcodesAvailableAsync([barcode], cancellationToken);

        if (request.IsPrimary)
        {
            foreach (var existing in product.Barcodes)
            {
                existing.IsPrimary = false;
            }
        }

        product.Barcodes.Add(new ProductBarcode
        {
            TenantId = currentTenant.TenantId,
            ProductId = product.Id,
            Barcode = barcode,
            IsPrimary = request.IsPrimary || product.Barcodes.Count == 0
        });

        auditWriter.AddSnapshot("product.barcode_added", nameof(Product), product.Id, oldValue, ProductSnapshot(product), new { barcode });
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(product);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var product = await FindProductAsync(id, cancellationToken);
        var oldValue = ProductSnapshot(product);
        product.IsActive = false;
        auditWriter.AddSnapshot("product.deactivated", nameof(Product), product.Id, oldValue, ProductSnapshot(product));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Product> FindProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.Barcodes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return product ?? throw new AppProblemException(404, "product_not_found", "Product was not found.");
    }

    private async Task<Category?> FindOrCreateCategoryAsync(string? categoryName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var name = categoryName.Trim();
        var normalized = name.ToLowerInvariant();
        var category = await dbContext.Categories.SingleOrDefaultAsync(
            x => x.Name.ToLower() == normalized,
            cancellationToken);

        if (category is not null)
        {
            return category;
        }

        category = new Category
        {
            TenantId = currentTenant.TenantId,
            Name = name
        };
        dbContext.Categories.Add(category);
        return category;
    }

    private async Task EnsureSkuAvailableAsync(string sku, Guid? currentProductId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Products.AnyAsync(
            x => x.Sku == sku && (!currentProductId.HasValue || x.Id != currentProductId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "sku_exists", "A product with this SKU already exists.");
        }
    }

    private async Task EnsureBarcodesAvailableAsync(IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        if (barcodes.Count == 0)
        {
            return;
        }

        var exists = await dbContext.ProductBarcodes.AnyAsync(x => barcodes.Contains(x.Barcode), cancellationToken);
        if (exists)
        {
            throw new AppProblemException(409, "barcode_exists", "One or more barcodes are already assigned to a product.");
        }
    }

    private static IReadOnlyList<string> NormalizeBarcodes(IReadOnlyCollection<string> barcodes)
    {
        return barcodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProductDto ToDto(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.Category?.Name,
            product.CriticalStockLevel,
            product.CurrentStock,
            product.IsActive,
            product.Barcodes.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Barcode).Select(x => x.Barcode).ToList());
    }

    private static object ProductSnapshot(Product product)
    {
        return new
        {
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.CategoryId,
            CategoryName = product.Category?.Name,
            product.CriticalStockLevel,
            product.CurrentStock,
            product.IsActive,
            Barcodes = product.Barcodes.Select(x => new { x.Barcode, x.IsPrimary }).ToList()
        };
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }
}
