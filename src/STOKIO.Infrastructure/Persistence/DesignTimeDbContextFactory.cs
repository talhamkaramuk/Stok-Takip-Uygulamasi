using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using STOKIO.Application.Abstractions;

namespace STOKIO.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StokioDbContext>
{
    public StokioDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STOKIO_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=stokio;Username=stokio;Password=stokio_dev_password";

        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StokioDbContext(options, new EmptyCurrentTenant());
    }

    private sealed class EmptyCurrentTenant : ICurrentTenant
    {
        public bool HasTenant => false;
        public Guid TenantId => Guid.Empty;
        public string? TenantSlug => null;
        public void SetTenant(Guid tenantId, string? slug)
        {
        }
    }
}

