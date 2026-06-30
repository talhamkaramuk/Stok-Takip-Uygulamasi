using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Exports;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlExportJobProcessorTests(PostgreSqlDatabaseFixture fixture)
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProcessNextAsync_ClaimsQueuedJobOnlyOnceAcrossConcurrentWorkers()
    {
        var tenantId = Guid.CreateVersion7();
        var storagePath = CreateTempStoragePath();

        try
        {
            await fixture.ResetAsync();
            await fixture.SeedTenantAsync(tenantId);
            var jobId = await SeedExportJobAsync(tenantId, ExportJobStatus.Queued);
            var configuration = CreateConfiguration(storagePath);
            var exportService = new CountingExportService();

            await using var firstContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            await using var secondContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));

            var results = await Task.WhenAll(
                CreateProcessor(firstContext, tenantId, exportService, configuration).ProcessNextAsync(CancellationToken.None),
                CreateProcessor(secondContext, tenantId, exportService, configuration).ProcessNextAsync(CancellationToken.None));

            Assert.Contains(results, processed => processed);
            Assert.Contains(results, processed => !processed);
            Assert.Equal(1, exportService.CurrentStockCalls);

            await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var job = await assertionContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal(ExportJobStatus.Ready, job.Status);
            Assert.Equal(1, job.RetryCount);
            Assert.Null(job.LockedBy);
            Assert.Null(job.LockedUntil);
            Assert.NotNull(job.StorageKey);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProcessNextAsync_ReclaimsExpiredProcessingJob()
    {
        var tenantId = Guid.CreateVersion7();
        var storagePath = CreateTempStoragePath();

        try
        {
            await fixture.ResetAsync();
            await fixture.SeedTenantAsync(tenantId);
            var jobId = await SeedExportJobAsync(
                tenantId,
                ExportJobStatus.Processing,
                retryCount: 1,
                lockedBy: "stopped-worker",
                lockedUntil: DateTimeOffset.UtcNow.AddMinutes(-5));
            var configuration = CreateConfiguration(storagePath);
            var exportService = new CountingExportService();

            await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var processed = await CreateProcessor(dbContext, tenantId, exportService, configuration)
                .ProcessNextAsync(CancellationToken.None);

            Assert.True(processed);
            Assert.Equal(1, exportService.CurrentStockCalls);

            await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var job = await assertionContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal(ExportJobStatus.Ready, job.Status);
            Assert.Equal(2, job.RetryCount);
            Assert.Null(job.LockedBy);
            Assert.Null(job.LockedUntil);
            Assert.NotNull(job.LastAttemptAt);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProcessNextAsync_SchedulesRetryWithBackoffAndSkipsUntilDue()
    {
        var tenantId = Guid.CreateVersion7();
        var storagePath = CreateTempStoragePath();
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await fixture.ResetAsync();
            await fixture.SeedTenantAsync(tenantId);
            var jobId = await SeedExportJobAsync(tenantId, ExportJobStatus.Queued);
            var configuration = CreateConfiguration(storagePath);
            var exportService = new FailingExportService();

            await using (var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId)))
            {
                var processed = await CreateProcessor(dbContext, tenantId, exportService, configuration, new TestClock(now))
                    .ProcessNextAsync(CancellationToken.None);

                Assert.True(processed);
            }

            await using (var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId)))
            {
                var processed = await CreateProcessor(dbContext, tenantId, exportService, configuration, new TestClock(now))
                    .ProcessNextAsync(CancellationToken.None);

                Assert.False(processed);
            }

            Assert.Equal(1, exportService.CurrentStockCalls);

            await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var job = await assertionContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal(ExportJobStatus.Queued, job.Status);
            Assert.Equal(1, job.RetryCount);
            Assert.Equal(now.AddSeconds(10), job.NextAttemptAt);
            Assert.Equal("export_generation_failed", job.FailedReasonCode);
            Assert.Null(job.CompletedAt);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task ProcessNextAsync_MarksJobFailedAfterMaxRetryCount()
    {
        var tenantId = Guid.CreateVersion7();
        var storagePath = CreateTempStoragePath();
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await fixture.ResetAsync();
            await fixture.SeedTenantAsync(tenantId);
            var jobId = await SeedExportJobAsync(
                tenantId,
                ExportJobStatus.Queued,
                retryCount: 2,
                maxRetryCount: 3);
            var configuration = CreateConfiguration(storagePath);
            var exportService = new FailingExportService();

            await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var processed = await CreateProcessor(dbContext, tenantId, exportService, configuration, new TestClock(now))
                .ProcessNextAsync(CancellationToken.None);

            Assert.True(processed);

            await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var job = await assertionContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal(ExportJobStatus.Failed, job.Status);
            Assert.Equal(3, job.RetryCount);
            Assert.Equal(now, job.CompletedAt);
            Assert.Null(job.NextAttemptAt);
            Assert.Equal("export_generation_failed", job.FailedReasonCode);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task CleanupExpiredAsync_DeletesExpiredFilesAndRetainedRows()
    {
        var tenantId = Guid.CreateVersion7();
        var storagePath = CreateTempStoragePath();
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await fixture.ResetAsync();
            await fixture.SeedTenantAsync(tenantId);
            var expiredReadyJobId = await SeedExportJobAsync(
                tenantId,
                ExportJobStatus.Ready,
                completedAt: now.AddHours(-1),
                expiresAt: now.AddMinutes(-1));
            var expiredStorageKey = CreateStoredExportFile(storagePath, tenantId, expiredReadyJobId);
            var retainedFailedJobId = await SeedExportJobAsync(
                tenantId,
                ExportJobStatus.Failed,
                completedAt: now.AddDays(-8),
                expiresAt: now.AddDays(-7));

            await SetStorageKeyAsync(tenantId, expiredReadyJobId, expiredStorageKey);
            var configuration = CreateConfiguration(storagePath);

            await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var cleaned = await CreateProcessor(dbContext, tenantId, new CountingExportService(), configuration, new TestClock(now))
                .CleanupExpiredAsync(CancellationToken.None);

            Assert.Equal(2, cleaned);
            Assert.False(File.Exists(Path.Combine(storagePath, tenantId.ToString("N"), $"{expiredReadyJobId:N}.xlsx")));

            await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
            var expiredReadyJob = await assertionContext.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == expiredReadyJobId);
            Assert.Null(expiredReadyJob.StorageKey);
            Assert.False(await assertionContext.ExportJobs.AsNoTracking().AnyAsync(x => x.Id == retainedFailedJobId));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    private async Task<Guid> SeedExportJobAsync(
        Guid tenantId,
        ExportJobStatus status,
        int retryCount = 0,
        int maxRetryCount = 3,
        string? lockedBy = null,
        DateTimeOffset? lockedUntil = null,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var job = new ExportJob
        {
            TenantId = tenantId,
            Type = ExportJobType.CurrentStock,
            Status = status,
            FileName = "stokio-current-stock.xlsx",
            ContentType = XlsxContentType,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount,
            LockedBy = lockedBy,
            LockedUntil = lockedUntil,
            CompletedAt = completedAt
        };

        dbContext.ExportJobs.Add(job);
        await dbContext.SaveChangesAsync();
        return job.Id;
    }

    private static ExportJobProcessor CreateProcessor(
        StokioDbContext dbContext,
        Guid tenantId,
        IExportService exportService,
        IConfiguration configuration,
        IClock? clock = null)
    {
        return new ExportJobProcessor(
            dbContext,
            new TestCurrentTenant(tenantId),
            exportService,
            new ExportJobFileStore(configuration),
            clock ?? new TestClock(DateTimeOffset.UtcNow),
            configuration,
            new NoopMetricsRecorder(),
            NullLogger<ExportJobProcessor>.Instance);
    }

    private static IConfiguration CreateConfiguration(string storagePath)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Exports:StoragePath"] = storagePath,
                ["Exports:JobLockTimeoutSeconds"] = "60",
                ["Exports:RetryBackoffBaseSeconds"] = "10",
                ["Exports:RetryBackoffMaxSeconds"] = "60",
                ["Exports:CompletedRetentionDays"] = "7"
            })
            .Build();
    }

    private async Task SetStorageKeyAsync(Guid tenantId, Guid jobId, string storageKey)
    {
        await using var dbContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        await dbContext.ExportJobs
            .Where(x => x.Id == jobId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.StorageKey, storageKey));
    }

    private static string CreateStoredExportFile(string storagePath, Guid tenantId, Guid jobId)
    {
        var tenantFolder = Path.Combine(storagePath, tenantId.ToString("N"));
        Directory.CreateDirectory(tenantFolder);
        var path = Path.Combine(tenantFolder, $"{jobId:N}.xlsx");
        File.WriteAllBytes(path, [1, 2, 3]);
        return $"{tenantId:N}/{jobId:N}.xlsx";
    }

    private static string CreateTempStoragePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stokio-export-tests-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempStoragePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class CountingExportService : IExportService
    {
        private int _currentStockCalls;

        public int CurrentStockCalls => Volatile.Read(ref _currentStockCalls);

        public async Task<ExportFile> CurrentStockAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _currentStockCalls);
            await Task.Delay(200, cancellationToken);
            return new ExportFile("stokio-current-stock.xlsx", XlsxContentType, [1, 2, 3]);
        }

        public Task<ExportFile> CriticalStockAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ExportFile> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ExportFile> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingExportService : IExportService
    {
        private int _currentStockCalls;

        public int CurrentStockCalls => Volatile.Read(ref _currentStockCalls);

        public Task<ExportFile> CurrentStockAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _currentStockCalls);
            throw new InvalidOperationException("Simulated export failure.");
        }

        public Task<ExportFile> CriticalStockAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ExportFile> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ExportFile> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoopMetricsRecorder : IMetricsRecorder
    {
        public void RecordRequest(int statusCode, double elapsedMs)
        {
        }

        public void RecordLogin(bool succeeded)
        {
        }

        public void RecordStockMovement(StockMovementType type, int quantity, bool isCriticalAfterMovement)
        {
        }

        public void RecordExport(ExportJobType type, bool succeeded, double elapsedMs)
        {
        }

        public void RecordDatabaseReadiness(bool succeeded, double elapsedMs)
        {
        }

        public MetricsSnapshotDto Snapshot()
        {
            return new MetricsSnapshotDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }
}
