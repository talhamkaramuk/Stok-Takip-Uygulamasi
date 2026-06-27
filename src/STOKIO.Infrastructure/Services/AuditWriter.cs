using System.Text.Json;
using STOKIO.Application.Abstractions;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class AuditWriter(StokioDbContext dbContext, ICurrentTenant currentTenant, ICurrentUser currentUser)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(string action, string entityName, Guid entityId, object? metadata = null)
    {
        AddInternal(action, entityName, entityId, oldValue: null, newValue: null, metadata);
    }

    public void AddSnapshot(string action, string entityName, Guid entityId, object? oldValue, object? newValue, object? metadata = null)
    {
        AddInternal(action, entityName, entityId, oldValue, newValue, metadata);
    }

    private void AddInternal(string action, string entityName, Guid entityId, object? oldValue, object? newValue, object? metadata)
    {
        if (!currentTenant.HasTenant)
        {
            return;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentTenant.TenantId,
            UserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId.ToString(),
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions)
        });
    }
}
