using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Categories;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Categories;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task CreateAsync_BlocksDuplicateCategoryNameWithinTenant()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var tenant = new TestCurrentTenant(tenantId);
        var service = new CategoryService(dbContext, tenant, new AuditWriter(dbContext, tenant, new TestCurrentUser()));

        await service.CreateAsync(new CreateCategoryRequest("Aksesuar"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            service.CreateAsync(new CreateCategoryRequest("aksesuar"), CancellationToken.None));

        Assert.Equal("category_exists", exception.Code);
    }

    private static StokioDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StokioDbContext(options, new TestCurrentTenant(tenantId));
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
