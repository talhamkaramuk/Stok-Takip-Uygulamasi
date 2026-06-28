using Microsoft.Extensions.Configuration;
using STOKIO.Infrastructure.Security;

namespace STOKIO.Tests.Security;

public sealed class JwtOptionsTests
{
    [Fact]
    public void FromConfiguration_DefaultsAccessTokenLifetimeToFifteenMinutes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "STOKIO.Api",
                ["Jwt:Audience"] = "STOKIO.Web",
                ["Jwt:SigningKey"] = "test-secret-value-with-at-least-32-bytes"
            })
            .Build();

        var options = JwtOptions.FromConfiguration(configuration);

        Assert.Equal(15, options.AccessTokenMinutes);
    }

    [Fact]
    public void FromConfiguration_UsesConfiguredAccessTokenLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "STOKIO.Api",
                ["Jwt:Audience"] = "STOKIO.Web",
                ["Jwt:SigningKey"] = "test-secret-value-with-at-least-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "7"
            })
            .Build();

        var options = JwtOptions.FromConfiguration(configuration);

        Assert.Equal(7, options.AccessTokenMinutes);
    }
}
