using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Parties;

namespace STOKIO.Application.Abstractions;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
