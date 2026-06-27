using STOKIO.Application.Dtos.Products;
using STOKIO.Application.Common;

namespace STOKIO.Application.Abstractions;

public interface IProductService
{
    Task<PagedResult<ProductDto>> ListAsync(string? search, Guid? categoryId, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> AddBarcodeAsync(Guid productId, AddBarcodeRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
