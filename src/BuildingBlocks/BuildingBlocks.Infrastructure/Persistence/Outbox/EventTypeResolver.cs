using System.Reflection;
using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Implementação baseada em reflection sobre os assemblies carregados no processo cujo
/// namespace raiz é "Advocacia" (o nome do assembly em si é só o nome do projeto, ex.:
/// "Platform.Domain" — a marca desacoplada só existe no namespace, ver ADR-023). O mapa é
/// montado sob demanda (Lazy) e não no construtor: no momento em que o singleton é criado
/// pelo container de DI, nem todo assembly de domínio (ex.: um módulo de negócio
/// referenciado só pelo Worker) necessariamente já foi carregado pelo runtime.
/// </summary>
public sealed class EventTypeResolver : IEventTypeResolver
{
    private readonly Lazy<IReadOnlyDictionary<string, Type>> _typesByName = new(BuildTypeMap);

    public Type Resolve(string eventType) =>
        _typesByName.Value.TryGetValue(eventType, out var type)
            ? type
            : throw new InvalidOperationException(
                $"Tipo de evento '{eventType}' não encontrado nos assemblies carregados. " +
                "Verifique se o assembly que o declara já foi referenciado/carregado.");

    private static IReadOnlyDictionary<string, Type> BuildTypeMap() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && type.Namespace?.StartsWith("Advocacia", StringComparison.Ordinal) == true
                && typeof(IDomainEvent).IsAssignableFrom(type))
            .GroupBy(type => type.FullName ?? type.Name)
            .ToDictionary(group => group.Key, group => group.First());

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}
