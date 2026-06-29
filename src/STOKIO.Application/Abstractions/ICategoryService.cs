using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Categories;

namespace STOKIO.Application.Abstractions;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
