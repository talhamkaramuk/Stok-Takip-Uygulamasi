namespace STOKIO.Tests.Relational;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!PostgreSqlTestSettings.IsEnabled)
        {
            Skip = "Set STOKIO_TEST_POSTGRES_CONNECTION_STRING to run PostgreSQL relational tests.";
        }
    }
}
