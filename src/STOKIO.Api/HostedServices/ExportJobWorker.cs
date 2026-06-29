using STOKIO.Application.Abstractions;

namespace STOKIO.Api.HostedServices;

public sealed class ExportJobWorker(
    IServiceProvider serviceProvider,
    IExportJobQueue exportJobQueue,
    ILogger<ExportJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid jobId;
            try
            {
                jobId = await exportJobQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessJobAsync(jobId, stoppingToken);
        }
    }

    private async Task RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IExportJobProcessor>();
        var pendingJobIds = await processor.RecoverPendingAsync(cancellationToken);

        foreach (var jobId in pendingJobIds)
        {
            await exportJobQueue.EnqueueAsync(jobId, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IExportJobProcessor>();
            await processor.ProcessAsync(jobId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Export job worker could not process job {ExportJobId}.", jobId);
        }
    }
}
