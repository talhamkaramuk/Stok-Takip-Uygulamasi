namespace STOKIO.Tests.Relational;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlDatabaseFixture>
{
    public const string Name = "PostgreSQL relational tests";
}
