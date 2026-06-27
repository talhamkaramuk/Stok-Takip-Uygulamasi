using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class IdempotencyRecord : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
