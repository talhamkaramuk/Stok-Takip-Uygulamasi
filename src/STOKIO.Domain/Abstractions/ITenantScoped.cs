namespace STOKIO.Domain.Abstractions;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

