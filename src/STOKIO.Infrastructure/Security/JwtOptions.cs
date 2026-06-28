using Microsoft.Extensions.Configuration;

namespace STOKIO.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var minutes = int.TryParse(section["AccessTokenMinutes"], out var parsedMinutes)
            ? parsedMinutes
            : 15;

        return new JwtOptions
        {
            Issuer = section["Issuer"] ?? string.Empty,
            Audience = section["Audience"] ?? string.Empty,
            SigningKey = section["SigningKey"] ?? string.Empty,
            AccessTokenMinutes = minutes
        };
    }
}
