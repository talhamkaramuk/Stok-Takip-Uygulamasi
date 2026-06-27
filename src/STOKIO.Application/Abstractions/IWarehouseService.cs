using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Warehouses;

namespace STOKIO.Application.Abstractions;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> ListAsync(bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken);
    Task<WarehouseDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<WarehouseStockDto>> ListStockAsync(Guid? warehouseId, Guid? productId, CancellationToken cancellationToken);
    Task<StockTransferDto> TransferAsync(StockTransferRequest request, CancellationToken cancellationToken);
}
