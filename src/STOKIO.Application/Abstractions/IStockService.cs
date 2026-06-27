using STOKIO.Application.Dtos.Stock;
using STOKIO.Application.Common;
using STOKIO.Domain.Enums;

namespace STOKIO.Application.Abstractions;

public interface IStockService
{
    Task<StockMovementDto> CreateMovementAsync(CreateStockMovementRequest request, CancellationToken cancellationToken);
    Task<PagedResult<StockMovementDto>> ListMovementsAsync(Guid? productId, Guid? warehouseId, StockMovementType? type, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<CriticalStockDto>> ListCriticalStockAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StockConsistencyDto>> CheckConsistencyAsync(CancellationToken cancellationToken);
}
