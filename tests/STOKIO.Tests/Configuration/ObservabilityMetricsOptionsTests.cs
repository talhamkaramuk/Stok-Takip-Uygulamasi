using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using STOKIO.Api.Configuration;

namespace STOKIO.Tests.Configuration;

public sealed class ObservabilityMetricsOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesStandardOtelEndpoint_WhenSectionEndpointIsBlank()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:Metrics:OtlpEndpoint"] = string.Empty,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector:4317"
            })
            .Build();

        var options = ObservabilityMetricsOptions.FromConfiguration(
            configuration,
            new TestHostEnvironment(Environments.Production));

        Assert.Equal(new Uri("http://otel-collector:4317"), options.OtlpEndpoint);
    }

    [Fact]
    public void FromConfiguration_EnablesDebugSnapshotEndpoint_ByDefaultOnlyInDevelopment()
    {
        var configuration = new ConfigurationBuilder().Build();

        var developmentOptions = ObservabilityMetricsOptions.FromConfiguration(
            configuration,
            new TestHostEnvironment(Environments.Development));
        var productionOptions = ObservabilityMetricsOptions.FromConfiguration(
            configuration,
            new TestHostEnvironment(Environments.Production));

        Assert.True(developmentOptions.EnableDebugSnapshotEndpoint);
        Assert.False(productionOptions.EnableDebugSnapshotEndpoint);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "STOKIO.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
