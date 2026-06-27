using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Categories;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class CategoryService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    AuditWriter auditWriter) : ICategoryService
{
    public async Task<PagedResult<CategoryDto>> ListAsync(bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        var query = dbContext.Categories.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var categories = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new CategoryDto(
                x.Id,
                x.Name,
                x.IsActive,
                dbContext.Products.Count(product => product.CategoryId == x.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<CategoryDto>(categories, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var category = new Category
        {
            TenantId = currentTenant.TenantId,
            Name = name
        };

        dbContext.Categories.Add(category);
        auditWriter.AddSnapshot("category.created", nameof(Category), category.Id, null, Snapshot(category, 0));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.IsActive, 0);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var category = await FindAsync(id, cancellationToken);
        var oldValue = Snapshot(category, await ProductCountAsync(category.Id, cancellationToken));
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, category.Id, cancellationToken);

        category.Name = name;
        category.IsActive = request.IsActive;

        var productCount = await ProductCountAsync(category.Id, cancellationToken);
        auditWriter.AddSnapshot("category.updated", nameof(Category), category.Id, oldValue, Snapshot(category, productCount));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.IsActive, productCount);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var category = await FindAsync(id, cancellationToken);
        var productCount = await ProductCountAsync(category.Id, cancellationToken);
        var oldValue = Snapshot(category, productCount);
        category.IsActive = false;
        auditWriter.AddSnapshot("category.deactivated", nameof(Category), category.Id, oldValue, Snapshot(category, productCount));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return category ?? throw new AppProblemException(404, "category_not_found", "Category was not found.");
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? currentCategoryId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLowerInvariant();
        var exists = await dbContext.Categories.AnyAsync(
            x => x.Name.ToLower() == normalized && (!currentCategoryId.HasValue || x.Id != currentCategoryId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "category_exists", "A category with this name already exists.");
        }
    }

    private Task<int> ProductCountAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return dbContext.Products.CountAsync(x => x.CategoryId == categoryId, cancellationToken);
    }

    private static object Snapshot(Category category, int productCount)
    {
        return new
        {
            category.Id,
            category.Name,
            category.IsActive,
            ProductCount = productCount
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
