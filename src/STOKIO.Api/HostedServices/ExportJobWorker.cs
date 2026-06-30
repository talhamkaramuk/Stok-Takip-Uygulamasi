using STOKIO.Application.Abstractions;

namespace STOKIO.Api.HostedServices;

public sealed class ExportJobWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<ExportJobWorker> logger) : BackgroundService
{
    private readonly TimeSpan _idleDelay = ResolveIdleDelay(configuration);
    private readonly TimeSpan _cleanupInterval = ResolveCleanupInterval(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextCleanupAt = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IExportJobProcessor>();

                if (DateTimeOffset.UtcNow >= nextCleanupAt)
                {
                    var cleaned = await processor.CleanupExpiredAsync(stoppingToken);
                    if (cleaned > 0)
                    {
                        logger.LogInformation("Export job cleanup removed {CleanedCount} expired artifacts or retained rows.", cleaned);
                    }

                    nextCleanupAt = DateTimeOffset.UtcNow.Add(_cleanupInterval);
                }

                var processed = await processor.ProcessNextAsync(stoppingToken);

                if (!processed)
                {
                    await Task.Delay(_idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Export job worker polling failed.");
                await Task.Delay(_idleDelay, stoppingToken);
            }
        }
    }

    private static TimeSpan ResolveIdleDelay(IConfiguration configuration)
    {
        var milliseconds = configuration.GetValue<int?>("Exports:WorkerIdleDelayMilliseconds");
        return milliseconds is > 0
            ? TimeSpan.FromMilliseconds(milliseconds.Value)
            : TimeSpan.FromSeconds(2);
    }

    private static TimeSpan ResolveCleanupInterval(IConfiguration configuration)
    {
        var minutes = configuration.GetValue<int?>("Exports:CleanupIntervalMinutes");
        return minutes is > 0
            ? TimeSpan.FromMinutes(minutes.Value)
            : TimeSpan.FromMinutes(30);
    }
}
