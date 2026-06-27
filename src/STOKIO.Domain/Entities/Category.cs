using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class Category : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

