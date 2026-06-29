using STOKIO.Application.Abstractions;

namespace STOKIO.Tests.Common;

public sealed class TestCurrentTenant(Guid tenantId, string? tenantSlug = "test") : ICurrentTenant
{
    public bool HasTenant => true;
    public Guid TenantId { get; private set; } = tenantId;
    public string? TenantSlug { get; private set; } = tenantSlug;

    public void SetTenant(Guid tenantId, string? slug)
    {
        TenantId = tenantId;
        TenantSlug = slug;
    }
}
