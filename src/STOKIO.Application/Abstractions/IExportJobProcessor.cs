namespace STOKIO.Application.Abstractions;

public interface IExportJobProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken);
}
