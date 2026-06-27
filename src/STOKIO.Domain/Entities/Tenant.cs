using STOKIO.Domain.Abstractions;

namespace STOKIO.Domain.Entities;

public sealed class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}
