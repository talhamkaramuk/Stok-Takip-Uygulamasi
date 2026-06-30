using STOKIO.Domain.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Domain.Entities;

public sealed class ExportJob : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? RequestedByUserId { get; set; }
    public ApplicationUser? RequestedByUser { get; set; }
    public ExportJobType Type { get; set; }
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Queued;
    public Guid? CountId { get; set; }
    public InventoryCount? Count { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FailedReasonCode { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; } = 3;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
