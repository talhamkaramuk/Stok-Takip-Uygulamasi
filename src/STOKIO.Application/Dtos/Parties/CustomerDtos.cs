namespace STOKIO.Application.Dtos.Parties;

public sealed record CreateCustomerRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxNumber,
    string? Address,
    string? Notes);

public sealed record UpdateCustomerRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? TaxNumber,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record CustomerDto(
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
