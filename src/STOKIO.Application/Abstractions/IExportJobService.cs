using STOKIO.Application.Dtos.Exports;

namespace STOKIO.Application.Abstractions;

public interface IExportJobService
{
    Task<ExportJobDto> CreateAsync(CreateExportJobRequest request, CancellationToken cancellationToken);
    Task<ExportJobDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ExportFile> DownloadAsync(Guid id, CancellationToken cancellationToken);
}
