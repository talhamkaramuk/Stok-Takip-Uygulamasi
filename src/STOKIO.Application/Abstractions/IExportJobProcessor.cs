namespace STOKIO.Application.Abstractions;

public interface IExportJobProcessor
{
    Task<IReadOnlyList<Guid>> RecoverPendingAsync(CancellationToken cancellationToken);
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken);
}
