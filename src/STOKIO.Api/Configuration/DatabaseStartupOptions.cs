using Microsoft.Extensions.Configuration;

namespace STOKIO.Api.Configuration;

public sealed record DatabaseStartupOptions(
    bool EnsureCreated,
    bool ApplyDevelopmentSchemaPatches,
    bool SeedDevelopmentData)
{
    public static DatabaseStartupOptions FromConfiguration(IConfiguration configuration)
    {
        var ensureCreated = ReadBoolean(configuration, "Database:EnsureCreated");
        var applyDevelopmentSchemaPatches = ReadBoolean(
            configuration,
            "Database:ApplyDevelopmentSchemaPatches",
            defaultValue: ensureCreated);
        var seedDevelopmentData = ReadBoolean(configuration, "Database:SeedDevelopmentData");

        return new DatabaseStartupOptions(
            ensureCreated,
            applyDevelopmentSchemaPatches,
            seedDevelopmentData);
    }

    private static bool ReadBoolean(IConfiguration configuration, string key, bool defaultValue = false)
    {
        return bool.TryParse(configuration[key], out var value) ? value : defaultValue;
    }
}
