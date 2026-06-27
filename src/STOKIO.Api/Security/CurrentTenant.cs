using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Security;

public sealed class CurrentTenant : ICurrentTenant
{
    private Guid _tenantId;

    public bool HasTenant { get; private set; }
    public Guid TenantId => HasTenant ? _tenantId : Guid.Empty;
    public string? TenantSlug { get; private set; }

    public void SetTenant(Guid tenantId, string? slug)
    {
        if (tenantId == Guid.Empty)
        {
            return;
        }

        _tenantId = tenantId;
        TenantSlug = slug;
        HasTenant = true;
    }
}

