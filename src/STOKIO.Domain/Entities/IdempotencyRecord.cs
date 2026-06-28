using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class IdempotencyRecord : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public IdempotencyRecordStatus Status { get; set; } = IdempotencyRecordStatus.Started;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string? ResponseSnapshotJson { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}
