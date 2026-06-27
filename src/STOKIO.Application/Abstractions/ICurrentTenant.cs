namespace STOKIO.Application.Abstractions;

public interface ICurrentTenant
{
    bool HasTenant { get; }
    Guid TenantId { get; }
    string? TenantSlug { get; }
    void SetTenant(Guid tenantId, string? slug);
}

