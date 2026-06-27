namespace STOKIO.Application.Dtos.Parties;

public sealed record CreateSupplierRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxNumber,
    string? Address,
    string? Notes);

public sealed record UpdateSupplierRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxNumber,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxNumber,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);
