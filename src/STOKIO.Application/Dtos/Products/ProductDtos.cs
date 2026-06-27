namespace STOKIO.Application.Dtos.Products;

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string? Description,
    string? CategoryName,
    int CriticalStockLevel,
    int InitialStock,
    IReadOnlyCollection<string> Barcodes);

public sealed record UpdateProductRequest(
    string Sku,
    string Name,
    string? Description,
    string? CategoryName,
    int CriticalStockLevel,
    bool IsActive);

public sealed record AddBarcodeRequest(
    string Barcode,
    bool IsPrimary);

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string? CategoryName,
    int CriticalStockLevel,
    int CurrentStock,
    bool IsActive,
    IReadOnlyList<string> Barcodes);

