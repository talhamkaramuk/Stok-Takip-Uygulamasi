namespace STOKIO.Application.Dtos.Categories;

public sealed record CreateCategoryRequest(string Name);

public sealed record UpdateCategoryRequest(string Name, bool IsActive);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    bool IsActive,
    int ProductCount);

