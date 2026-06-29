using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOKIO.Application.Abstractions;
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
    IMetricsRecorder metricsRecorder,
    ILogger<ExportJobProcessor> logger) : IExportJobProcessor
{
    public async Task<IReadOnlyList<Guid>> RecoverPendingAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var jobs = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .Where(x => (x.Status == ExportJobStatus.Queued || x.Status == ExportJobStatus.Processing)
                && x.ExpiresAt > now)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs.Where(x => x.Status == ExportJobStatus.Processing))
        {
            job.Status = ExportJobStatus.Queued;
            job.ErrorMessage = null;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return jobs.Select(x => x.Id).ToArray();
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.ExportJobs
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null || job.Status is ExportJobStatus.Ready or ExportJobStatus.Failed)
        {
            return;
        }

        currentTenant.SetTenant(job.TenantId, null);
        job.Status = ExportJobStatus.Processing;
        job.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var file = await CreateFileAsync(job, cancellationToken);
            var storageKey = await fileStore.SaveAsync(job.TenantId, job.Id, file, cancellationToken);

            job.FileName = file.FileName;
            job.ContentType = file.ContentType;
            job.StorageKey = storageKey;
            job.Status = ExportJobStatus.Ready;
            job.CompletedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            metricsRecorder.RecordExport(job.Type, succeeded: true, System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = ExportJobStatus.Queued;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Export job {ExportJobId} failed.", job.Id);
            job.Status = ExportJobStatus.Failed;
            job.ErrorMessage = "Dışa aktarma dosyası oluşturulamadı.";
            job.CompletedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            metricsRecorder.RecordExport(job.Type, succeeded: false, System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
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
}
