using Microsoft.Extensions.Configuration;

namespace STOKIO.Infrastructure.Security;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; set; } = 14;

    public static AuthOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var refreshTokenDays = int.TryParse(section["RefreshTokenDays"], out var parsedDays)
            ? parsedDays
            : 14;
        if (refreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Auth:RefreshTokenDays must be greater than zero.");
        }

        return new AuthOptions
        {
            RefreshTokenDays = refreshTokenDays
        };
    }
}
