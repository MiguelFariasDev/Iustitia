namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Compartilha um único host de Platform.Api (e o Postgres/Testcontainers por trás dele)
/// entre todas as classes de teste HTTP desta collection — cada instância de
/// <see cref="PlatformApiFactory"/> sobe um container, caro para repetir por classe.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlatformApiCollection : ICollectionFixture<PlatformApiFactory>
{
    public const string Name = "PlatformApi";
}
