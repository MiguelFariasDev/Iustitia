using System.Collections.Concurrent;
using Advocacia.BuildingBlocks.Infrastructure.Caching;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.DataJud;

/// <summary>
/// Cache em memória para os testes do DataJudClient. Guarda os objetos como estão (sem
/// serializar): o que se testa aqui é a decisão de consultar ou não, não o serializador.
/// </summary>
internal sealed class FakeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object?> _entries = new();

    internal int Writes { get; private set; }

    internal bool Contains(string key) => _entries.ContainsKey(key);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        _entries[key] = value;
        Writes++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            return (T)existing!;
        }

        var created = await factory(cancellationToken);
        await SetAsync(key, created, expiration, cancellationToken);
        return created;
    }
}
