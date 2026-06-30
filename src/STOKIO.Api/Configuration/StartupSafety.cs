using STOKIO.Infrastructure.Security;

namespace STOKIO.Api.Configuration;

public static class StartupSafety
{
    private const string DevelopmentSigningKey = "development-only-change-this-32-byte-minimum-key";

    public static void Validate(
        IHostEnvironment environment,
        JwtOptions jwtOptions,
        IReadOnlyCollection<string> allowedOrigins,
        DatabaseStartupOptions databaseOptions,
        ObservabilityMetricsOptions observabilityMetricsOptions)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        if (databaseOptions.EnsureCreated)
        {
            throw new InvalidOperationException("Database:EnsureCreated can only be enabled in Development.");
        }

        if (databaseOptions.ApplyDevelopmentSchemaPatches)
        {
            throw new InvalidOperationException("Database:ApplyDevelopmentSchemaPatches can only be enabled in Development.");
        }

        if (databaseOptions.SeedDevelopmentData)
        {
            throw new InvalidOperationException("Database:SeedDevelopmentData can only be enabled in Development.");
        }

        if (allowedOrigins.Count == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured outside Development.");
        }

        if (allowedOrigins.Any(IsWildcardOrigin))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins cannot contain wildcard origins outside Development.");
        }

        if (IsDevelopmentJwtSigningKey(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey must be supplied by a production secret source outside Development.");
        }

        if (!observabilityMetricsOptions.HasExternalExporter)
        {
            throw new InvalidOperationException(
                "Observability:Metrics:OtlpEndpoint or OTEL_EXPORTER_OTLP_ENDPOINT must be configured outside Development.");
        }
    }

    private static bool IsWildcardOrigin(string origin)
    {
        return origin == "*" || origin.Contains('*', StringComparison.Ordinal);
    }

    private static bool IsDevelopmentJwtSigningKey(string signingKey)
    {
        return string.Equals(signingKey, DevelopmentSigningKey, StringComparison.Ordinal)
            || signingKey.Contains("development", StringComparison.OrdinalIgnoreCase)
            || signingKey.Contains("change-this", StringComparison.OrdinalIgnoreCase);
    }
}
