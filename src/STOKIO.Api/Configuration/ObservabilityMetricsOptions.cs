namespace STOKIO.Api.Configuration;

public sealed record ObservabilityMetricsOptions(
    bool EnableDebugSnapshotEndpoint,
    Uri? OtlpEndpoint)
{
    public const string SectionName = "Observability:Metrics";

    public bool HasExternalExporter => OtlpEndpoint is not null;

    public static ObservabilityMetricsOptions FromConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(SectionName);
        var enableDebugSnapshotEndpoint = section.GetValue<bool?>("EnableDebugSnapshotEndpoint")
            ?? environment.IsDevelopment();
        var endpointValue = section["OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            endpointValue = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        }

        return new ObservabilityMetricsOptions(
            enableDebugSnapshotEndpoint,
            ParseOptionalEndpoint(endpointValue));
    }

    private static Uri? ParseOptionalEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException($"{SectionName}:OtlpEndpoint must be an absolute URI.");
        }

        return endpoint;
    }
}
