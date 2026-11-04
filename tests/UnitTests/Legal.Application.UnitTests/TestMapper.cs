using Mapster;
using MapsterMapper;

namespace Advocacia.Modules.Legal.Application.UnitTests;

/// <summary>
/// IMapper real com os profiles de Legal.Application (ver PublicationMappingProfile) — mockar
/// IMapper esconderia justamente o que interessa validar através do handler: que o RawContent
/// não escapa numa listagem e que os value objects são desempacotados.
/// Usa um TypeAdapterConfig isolado do global (mesmo padrão de Platform.Application.UnitTests).
/// </summary>
public static class TestMapper
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        config.Scan(LegalApplicationAssembly.Reference);

        return new Mapper(config);
    }
}
