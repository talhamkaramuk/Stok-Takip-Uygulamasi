using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Domain.Enums;

namespace STOKIO.Application.Abstractions;

public interface ISalesOrderService
{
    Task<PagedResult<SalesOrderDto>> ListAsync(SalesOrderStatus? status, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken);
}

public interface IPurchaseRequestService
{
    Task<PagedResult<PurchaseRequestDto>> ListAsync(PurchaseRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, CancellationToken cancellationToken);
    Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseRequestDto> ReceiveAsync(Guid id, ReceivePurchaseRequestRequest? request, CancellationToken cancellationToken);
}

public interface IShipmentService
{
    Task<PagedResult<ShipmentDto>> ListAsync(ShipmentStatus? status, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<ShipmentDto> CreateAsync(CreateShipmentRequest request, CancellationToken cancellationToken);
}

public interface IReturnRequestService
{
    Task<PagedResult<ReturnRequestDto>> ListAsync(ReturnRequestStatus? status, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<ReturnRequestDto> CreateAsync(CreateReturnRequestRequest request, CancellationToken cancellationToken);
}
