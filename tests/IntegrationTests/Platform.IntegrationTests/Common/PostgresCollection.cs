namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Compartilha um único container Postgres (e as migrations já aplicadas) entre
/// todas as classes de teste desta collection — subir um container por classe seria
/// caro (cada um leva alguns segundos para inicializar).
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
