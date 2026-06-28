using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class IdempotencyService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    IIdempotencyKeyAccessor? keyAccessor = null)
{
    private const int MaxKeyLength = 160;
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IdempotencyRecord?> TryReserveAsync(
        string operationName,
        object requestFingerprint,
        CancellationToken cancellationToken)
    {
        var key = CurrentKey();
        if (key is null)
        {
            return null;
        }

        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }

        var requestHash = Hash(requestFingerprint);
        var existing = await FindExistingRecordAsync(operationName, key, cancellationToken);
        if (existing is not null)
        {
            return ValidateExisting(existing, requestHash);
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var recordId = Guid.CreateVersion7();
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.Add(ReservationLifetime);
            const string status = nameof(IdempotencyRecordStatus.Started);

            var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "IdempotencyRecords" (
                    "Id",
                    "CreatedAt",
                    "UpdatedAt",
                    "TenantId",
                    "Key",
                    "OperationName",
                    "RequestHash",
                    "Status",
                    "ResourceType",
                    "ResourceId",
                    "ResponseSnapshotJson",
                    "CompletedAt",
                    "ExpiresAt")
                VALUES (
                    {recordId},
                    {now},
                    NULL,
                    {currentTenant.TenantId},
                    {key},
                    {operationName},
                    {requestHash},
                    {status},
                    '',
                    '',
                    NULL,
                    NULL,
                    {expiresAt})
                ON CONFLICT ("TenantId", "OperationName", "Key") DO NOTHING
                """, cancellationToken);

            if (inserted == 1)
            {
                return null;
            }

            var concurrentRecord = await FindExistingRecordAsync(operationName, key, cancellationToken)
                ?? throw new AppProblemException(409, "idempotency_reservation_conflict", "Idempotency reservation could not be acquired.");

            return ValidateExisting(concurrentRecord, requestHash);
        }

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            TenantId = currentTenant.TenantId,
            Key = key,
            OperationName = operationName,
            RequestHash = requestHash,
            Status = IdempotencyRecordStatus.Started,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ReservationLifetime)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<bool> CompleteAsync<TResponse>(
        string operationName,
        object requestFingerprint,
        string resourceType,
        string resourceId,
        TResponse responseSnapshot,
        CancellationToken cancellationToken)
    {
        var key = CurrentKey();
        if (key is null)
        {
            return false;
        }

        var requestHash = Hash(requestFingerprint);
        var record = await dbContext.IdempotencyRecords
            .SingleOrDefaultAsync(x => x.OperationName == operationName && x.Key == key, cancellationToken);

        if (record is null)
        {
            throw new AppProblemException(409, "idempotency_reservation_missing", "Idempotency reservation was not found.");
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new AppProblemException(
                409,
                "idempotency_key_conflict",
                "Idempotency key was already used with a different request.");
        }

        record.Status = IdempotencyRecordStatus.Completed;
        record.ResourceType = resourceType;
        record.ResourceId = resourceId;
        record.ResponseSnapshotJson = JsonSerializer.Serialize(responseSnapshot, JsonOptions);
        record.CompletedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public TResponse? TryReadResponseSnapshot<TResponse>(IdempotencyRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ResponseSnapshotJson))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TResponse>(record.ResponseSnapshotJson, JsonOptions);
    }

    private async Task<IdempotencyRecord?> FindExistingRecordAsync(
        string operationName,
        string key,
        CancellationToken cancellationToken)
    {
        return await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationName == operationName && x.Key == key, cancellationToken);
    }

    private static IdempotencyRecord? ValidateExisting(IdempotencyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new AppProblemException(
                409,
                "idempotency_key_conflict",
                "Idempotency key was already used with a different request.");
        }

        if (record.Status == IdempotencyRecordStatus.Completed)
        {
            return record;
        }

        if (record.Status == IdempotencyRecordStatus.Started)
        {
            throw new AppProblemException(
                409,
                "idempotency_key_in_progress",
                "An operation with the same idempotency key is still in progress. Retry after the first request completes.");
        }

        throw new AppProblemException(
            409,
            "idempotency_key_failed",
            "Idempotency key is attached to a failed operation.");
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
