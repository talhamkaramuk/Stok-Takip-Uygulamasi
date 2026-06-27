using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using STOKIO.Api.Configuration;
using STOKIO.Infrastructure.Security;

namespace STOKIO.Tests.Configuration;

public sealed class StartupSafetyTests
{
    [Fact]
    public void Validate_AllowsDevelopmentOnlyDatabaseOptions_InDevelopment()
    {
        var exception = Record.Exception(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Development),
            CreateJwtOptions("development-only-change-this-32-byte-minimum-key"),
            ["*"],
            new DatabaseStartupOptions(
                EnsureCreated: true,
                ApplyDevelopmentSchemaPatches: true,
                SeedDevelopmentData: true)));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsEnsureCreatedOutsideDevelopment()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions(),
            ["https://app.stokio.local"],
            new DatabaseStartupOptions(
                EnsureCreated: true,
                ApplyDevelopmentSchemaPatches: false,
                SeedDevelopmentData: false)));

        Assert.Contains("Database:EnsureCreated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsDevelopmentSchemaPatchesOutsideDevelopment()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions(),
            ["https://app.stokio.local"],
            new DatabaseStartupOptions(
                EnsureCreated: false,
                ApplyDevelopmentSchemaPatches: true,
                SeedDevelopmentData: false)));

        Assert.Contains("Database:ApplyDevelopmentSchemaPatches", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsDevelopmentSeedOutsideDevelopment()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions(),
            ["https://app.stokio.local"],
            new DatabaseStartupOptions(
                EnsureCreated: false,
                ApplyDevelopmentSchemaPatches: false,
                SeedDevelopmentData: true)));

        Assert.Contains("Database:SeedDevelopmentData", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("https://*.stokio.local")]
    public void Validate_RejectsInvalidCorsOutsideDevelopment(string origin)
    {
        var origins = string.IsNullOrWhiteSpace(origin)
            ? []
            : new[] { origin };

        var exception = Assert.Throws<InvalidOperationException>(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions(),
            origins,
            new DatabaseStartupOptions(
                EnsureCreated: false,
                ApplyDevelopmentSchemaPatches: false,
                SeedDevelopmentData: false)));

        Assert.Contains("Cors:AllowedOrigins", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsDevelopmentJwtSigningKeyOutsideDevelopment()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions("development-only-change-this-32-byte-minimum-key"),
            ["https://app.stokio.local"],
            new DatabaseStartupOptions(
                EnsureCreated: false,
                ApplyDevelopmentSchemaPatches: false,
                SeedDevelopmentData: false)));

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllowsProductionReadyConfigurationOutsideDevelopment()
    {
        var exception = Record.Exception(() => StartupSafety.Validate(
            new TestHostEnvironment(Environments.Production),
            CreateJwtOptions(),
            ["https://app.stokio.local"],
            new DatabaseStartupOptions(
                EnsureCreated: false,
                ApplyDevelopmentSchemaPatches: false,
                SeedDevelopmentData: false)));

        Assert.Null(exception);
    }

    private static JwtOptions CreateJwtOptions(
        string signingKey = "prod-secret-value-with-at-least-32-bytes")
    {
        return new JwtOptions
        {
            Issuer = "STOKIO.Api",
            Audience = "STOKIO.Web",
            SigningKey = signingKey,
            AccessTokenMinutes = 60
        };
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "STOKIO.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
