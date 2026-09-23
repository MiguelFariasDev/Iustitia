using Mapster;
using MapsterMapper;

namespace Advocacia.Platform.Application.UnitTests;

/// <summary>
/// Instância real de IMapper para os testes de handlers que dependem de mapeamento
/// Domain -> DTO (ver Platform.Application/Mapping) — evita mockar IMapper (o próprio
/// mapeamento configurado é o que queremos validar implicitamente através do handler).
/// Usa um TypeAdapterConfig próprio, isolado do global, para não depender de
/// AddPlatformApplication já ter rodado no processo de teste.
/// </summary>
public static class TestMapper
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        config.Scan(PlatformApplicationAssembly.Reference);

        return new Mapper(config);
    }
}
