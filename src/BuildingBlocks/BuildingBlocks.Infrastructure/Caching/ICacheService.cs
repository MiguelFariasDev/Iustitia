namespace Advocacia.BuildingBlocks.Infrastructure.Caching;

/// <summary>Abstração de cache distribuído (Cache-Aside) sobre Azure Cache for Redis.</summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Busca no cache; se ausente, executa <paramref name="factory"/>, grava o resultado e o retorna.</summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);
}
