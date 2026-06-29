using Npgsql;

namespace STOKIO.Tests.Relational;

public static class PostgreSqlTestSettings
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("STOKIO_TEST_POSTGRES_CONNECTION_STRING");

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(ConnectionString);

    public static void EnsureSafeForDestructiveReset()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("STOKIO_TEST_POSTGRES_CONNECTION_STRING is required.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database ?? string.Empty;
        if (!databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Relational tests reset the target database. Use a dedicated database whose name contains 'test'.");
        }
    }
}
