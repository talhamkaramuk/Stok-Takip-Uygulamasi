using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class IdempotencyService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    IIdempotencyKeyAccessor? keyAccessor = null)
{
    private const int MaxKeyLength = 160;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IdempotencyRecord?> FindExistingAsync(
        string operationName,
        object requestFingerprint,
        CancellationToken cancellationToken)
    {
        var key = CurrentKey();
        if (key is null)
        {
            return null;
        }

        var requestHash = Hash(requestFingerprint);
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationName == operationName && x.Key == key, cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new AppProblemException(
                409,
                "idempotency_key_conflict",
                "Idempotency key was already used with a different request.");
        }

        return record;
    }

    public void AddCompleted(
        string operationName,
        object requestFingerprint,
        string resourceType,
        string resourceId)
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }

        var key = CurrentKey();
        if (key is null)
        {
            return;
        }

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            TenantId = currentTenant.TenantId,
            Key = key,
            OperationName = operationName,
            RequestHash = Hash(requestFingerprint),
            ResourceType = resourceType,
            ResourceId = resourceId
        });
    }

    private string? CurrentKey()
    {
        var key = keyAccessor?.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        if (key.Length > MaxKeyLength)
        {
            throw new AppProblemException(
                400,
                "idempotency_key_invalid",
                $"Idempotency key cannot exceed {MaxKeyLength} characters.");
        }

        return key;
    }

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }
}
