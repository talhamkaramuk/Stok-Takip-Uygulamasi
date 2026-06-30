using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Exports;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class ExportJobProcessor(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    IExportService exportService,
    ExportJobFileStore fileStore,
    IClock clock,
    IConfiguration configuration,
    IMetricsRecorder metricsRecorder,
    ILogger<ExportJobProcessor> logger) : IExportJobProcessor
{
    private const string ExportFailedMessage = "Disa aktarma dosyasi olusturulamadi.";
    private const string ExportFailedReasonCode = "export_generation_failed";
    private readonly TimeSpan _lockTimeout = ResolveLockTimeout(configuration);
    private readonly TimeSpan _retryBackoffBase = ResolveRetryBackoffBase(configuration);
    private readonly TimeSpan _retryBackoffMax = ResolveRetryBackoffMax(configuration);
    private readonly TimeSpan _completedRetention = ResolveCompletedRetention(configuration);
    private readonly string _workerId = ResolveWorkerId(configuration);

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await ClaimNextAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        currentTenant.SetTenant(job.TenantId, null);

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var file = await CreateFileAsync(job, cancellationToken);
            var storageKey = await fileStore.SaveAsync(job.TenantId, job.Id, file, cancellationToken);

            var completed = await CompleteJobAsync(job, file, storageKey, cancellationToken);
            if (!completed)
            {
                logger.LogWarning("Export job {ExportJobId} was completed by stale worker {WorkerId}; database state was not overwritten.", job.Id, _workerId);
            }

            metricsRecorder.RecordExport(job.Type, succeeded: true, System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseClaimAsync(job, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Export job {ExportJobId} failed on attempt {Attempt}/{MaxAttempts}.", job.Id, job.RetryCount, job.MaxRetryCount);

            var recorded = await RecordFailedAttemptAsync(job, CancellationToken.None);
            if (!recorded)
            {
                logger.LogWarning("Export job {ExportJobId} failure came from stale worker {WorkerId}; database state was not overwritten.", job.Id, _workerId);
            }

            metricsRecorder.RecordExport(job.Type, succeeded: false, System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return true;
        }
    }

    private async Task<ExportJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var lockedUntil = now.Add(_lockTimeout);
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE "ExportJobs"
                SET "Status" = @processingStatus,
                    "LockedBy" = @lockedBy,
                    "LockedUntil" = @lockedUntil,
                    "RetryCount" = "RetryCount" + 1,
                    "LastAttemptAt" = @now,
                    "NextAttemptAt" = NULL,
                    "ErrorMessage" = NULL,
                    "FailedReasonCode" = NULL,
                    "UpdatedAt" = @now
                WHERE "Id" = (
                    SELECT "Id"
                    FROM "ExportJobs"
                    WHERE "ExpiresAt" > @now
                      AND "RetryCount" < "MaxRetryCount"
                      AND (
                          (
                              "Status" = @queuedStatus
                              AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= @now)
                          )
                          OR (
                              "Status" = @processingStatus
                              AND ("LockedUntil" IS NULL OR "LockedUntil" <= @now)
                          )
                      )
                    ORDER BY "NextAttemptAt" NULLS FIRST, "CreatedAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                )
                RETURNING
                    "Id",
                    "CreatedAt",
                    "UpdatedAt",
                    "TenantId",
                    "RequestedByUserId",
                    "Type",
                    "Status",
                    "CountId",
                    "From",
                    "To",
                    "FileName",
                    "ContentType",
                    "StorageKey",
                    "ErrorMessage",
                    "FailedReasonCode",
                    "LockedBy",
                    "LockedUntil",
                    "RetryCount",
                    "MaxRetryCount",
                    "LastAttemptAt",
                    "NextAttemptAt",
                    "CompletedAt",
                    "ExpiresAt";
                """;

            AddParameter(command, "queuedStatus", ExportJobStatus.Queued.ToString());
            AddParameter(command, "processingStatus", ExportJobStatus.Processing.ToString());
            AddParameter(command, "lockedBy", _workerId);
            AddParameter(command, "lockedUntil", lockedUntil);
            AddParameter(command, "now", now);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var job = ReadExportJob(reader);
            return job;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> CompleteJobAsync(
        ExportJob job,
        ExportFile file,
        string storageKey,
        CancellationToken cancellationToken)
    {
        var completedAt = clock.UtcNow;
        var affectedRows = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .Where(x => x.Id == job.Id
                && x.Status == ExportJobStatus.Processing
                && x.LockedBy == _workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.FileName, file.FileName)
                .SetProperty(x => x.ContentType, file.ContentType)
                .SetProperty(x => x.StorageKey, storageKey)
                .SetProperty(x => x.Status, ExportJobStatus.Ready)
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.FailedReasonCode, (string?)null)
                .SetProperty(x => x.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(x => x.CompletedAt, completedAt)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTimeOffset?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    private async Task ReleaseClaimAsync(ExportJob job, CancellationToken cancellationToken)
    {
        await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .Where(x => x.Id == job.Id
                && x.Status == ExportJobStatus.Processing
                && x.LockedBy == _workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ExportJobStatus.Queued)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTimeOffset?)null),
                cancellationToken);
    }

    private async Task<bool> RecordFailedAttemptAsync(ExportJob job, CancellationToken cancellationToken)
    {
        var finalFailure = job.RetryCount >= job.MaxRetryCount;
        var nextStatus = finalFailure ? ExportJobStatus.Failed : ExportJobStatus.Queued;
        var now = clock.UtcNow;
        DateTimeOffset? completedAt = finalFailure ? now : null;
        DateTimeOffset? nextAttemptAt = finalFailure ? null : now.Add(CalculateBackoff(job.RetryCount));

        var affectedRows = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .Where(x => x.Id == job.Id
                && x.Status == ExportJobStatus.Processing
                && x.LockedBy == _workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, nextStatus)
                .SetProperty(x => x.ErrorMessage, ExportFailedMessage)
                .SetProperty(x => x.FailedReasonCode, ExportFailedReasonCode)
                .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
                .SetProperty(x => x.CompletedAt, completedAt)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTimeOffset?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expiredFiles = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Status == ExportJobStatus.Ready
                && x.ExpiresAt <= now
                && x.StorageKey != null)
            .Select(x => new { x.Id, x.StorageKey })
            .ToListAsync(cancellationToken);

        var cleaned = 0;
        foreach (var expiredFile in expiredFiles)
        {
            try
            {
                fileStore.DeleteIfExists(expiredFile.StorageKey!);
                var affectedRows = await dbContext.ExportJobs
                    .IgnoreQueryFilters()
                    .Where(x => x.Id == expiredFile.Id && x.StorageKey == expiredFile.StorageKey)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.StorageKey, (string?)null)
                        .SetProperty(x => x.UpdatedAt, now),
                        cancellationToken);

                cleaned += affectedRows;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AppProblemException)
            {
                logger.LogWarning(exception, "Expired export file cleanup failed for job {ExportJobId}.", expiredFile.Id);
            }
        }

        var retentionCutoff = now.Subtract(_completedRetention);
        var retainedFiles = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => (x.Status == ExportJobStatus.Ready || x.Status == ExportJobStatus.Failed)
                && x.CompletedAt != null
                && x.CompletedAt <= retentionCutoff
                && x.StorageKey != null)
            .Select(x => x.StorageKey!)
            .ToListAsync(cancellationToken);

        foreach (var storageKey in retainedFiles)
        {
            try
            {
                fileStore.DeleteIfExists(storageKey);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AppProblemException)
            {
                logger.LogWarning(exception, "Retained export file cleanup failed for storage key {StorageKey}.", storageKey);
            }
        }

        cleaned += await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .Where(x => (x.Status == ExportJobStatus.Ready || x.Status == ExportJobStatus.Failed)
                && x.CompletedAt != null
                && x.CompletedAt <= retentionCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return cleaned;
    }

    private Task<ExportFile> CreateFileAsync(
        ExportJob job,
        CancellationToken cancellationToken)
    {
        return job.Type switch
        {
            ExportJobType.CurrentStock => exportService.CurrentStockAsync(cancellationToken),
            ExportJobType.CriticalStock => exportService.CriticalStockAsync(cancellationToken),
            ExportJobType.StockMovements => exportService.MovementsAsync(job.From, job.To, cancellationToken),
            ExportJobType.CountDifferences when job.CountId.HasValue =>
                exportService.CountDifferencesAsync(job.CountId.Value, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported export job type.")
        };
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static ExportJob ReadExportJob(DbDataReader reader)
    {
        return new ExportJob
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = GetNullable<DateTimeOffset>(reader, "UpdatedAt"),
            TenantId = reader.GetGuid(reader.GetOrdinal("TenantId")),
            RequestedByUserId = GetNullable<Guid>(reader, "RequestedByUserId"),
            Type = Enum.Parse<ExportJobType>(reader.GetString(reader.GetOrdinal("Type"))),
            Status = Enum.Parse<ExportJobStatus>(reader.GetString(reader.GetOrdinal("Status"))),
            CountId = GetNullable<Guid>(reader, "CountId"),
            From = GetNullable<DateTimeOffset>(reader, "From"),
            To = GetNullable<DateTimeOffset>(reader, "To"),
            FileName = reader.GetString(reader.GetOrdinal("FileName")),
            ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
            StorageKey = GetNullableString(reader, "StorageKey"),
            ErrorMessage = GetNullableString(reader, "ErrorMessage"),
            FailedReasonCode = GetNullableString(reader, "FailedReasonCode"),
            LockedBy = GetNullableString(reader, "LockedBy"),
            LockedUntil = GetNullable<DateTimeOffset>(reader, "LockedUntil"),
            RetryCount = reader.GetInt32(reader.GetOrdinal("RetryCount")),
            MaxRetryCount = reader.GetInt32(reader.GetOrdinal("MaxRetryCount")),
            LastAttemptAt = GetNullable<DateTimeOffset>(reader, "LastAttemptAt"),
            NextAttemptAt = GetNullable<DateTimeOffset>(reader, "NextAttemptAt"),
            CompletedAt = GetNullable<DateTimeOffset>(reader, "CompletedAt"),
            ExpiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ExpiresAt"))
        };
    }

    private static T? GetNullable<T>(DbDataReader reader, string name)
        where T : struct
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    }

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static TimeSpan ResolveLockTimeout(IConfiguration configuration)
    {
        return TimeSpan.FromSeconds(ResolvePositiveInt(configuration, "Exports:JobLockTimeoutSeconds", 900));
    }

    private static TimeSpan ResolveRetryBackoffBase(IConfiguration configuration)
    {
        return TimeSpan.FromSeconds(ResolvePositiveInt(configuration, "Exports:RetryBackoffBaseSeconds", 30));
    }

    private static TimeSpan ResolveRetryBackoffMax(IConfiguration configuration)
    {
        return TimeSpan.FromSeconds(ResolvePositiveInt(configuration, "Exports:RetryBackoffMaxSeconds", 900));
    }

    private static TimeSpan ResolveCompletedRetention(IConfiguration configuration)
    {
        return TimeSpan.FromDays(ResolvePositiveInt(configuration, "Exports:CompletedRetentionDays", 7));
    }

    private TimeSpan CalculateBackoff(int attemptNumber)
    {
        var exponent = Math.Clamp(attemptNumber - 1, 0, 10);
        var seconds = _retryBackoffBase.TotalSeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(seconds, _retryBackoffMax.TotalSeconds));
    }

    private static int ResolvePositiveInt(IConfiguration configuration, string key, int fallback)
    {
        return int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    }

    private static string ResolveWorkerId(IConfiguration configuration)
    {
        var configured = configuration["Exports:WorkerId"];
        var workerId = string.IsNullOrWhiteSpace(configured)
            ? $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.CreateVersion7():N}"
            : configured.Trim();

        return workerId.Length <= 128 ? workerId : workerId[..128];
    }
}
