using STOKIO.Application.Dtos.Counts;

namespace STOKIO.Application.Abstractions;

public interface IInventoryCountService
{
    Task<InventoryCountDto> CreateAsync(CreateInventoryCountRequest request, CancellationToken cancellationToken);
    Task<InventoryCountDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<InventoryCountItemDto> ScanAsync(Guid countId, ScanCountItemRequest request, CancellationToken cancellationToken);
    Task<InventoryCountDto> CloseAsync(Guid countId, CloseInventoryCountRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryCountDifferenceDto>> GetDifferencesAsync(Guid countId, CancellationToken cancellationToken);
}

