namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>
/// Compartilha um único container Postgres (com todas as migrations já aplicadas) entre as
/// classes de teste desta collection — subir um container por classe custaria alguns segundos
/// cada.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LegalPostgresCollection : ICollectionFixture<LegalPostgresFixture>
{
    public const string Name = "LegalPostgres";
}
