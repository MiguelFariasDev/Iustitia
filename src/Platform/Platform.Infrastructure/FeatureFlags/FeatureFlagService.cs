using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.FeatureFlags;

/// <summary>
/// Lê feature_flags diretamente via DbContext (IgnoreQueryFilters: o tenantId já vem
/// explícito no parâmetro, então não faz sentido também restringir pelo tenant ambiente do
/// filtro global — podem divergir, ex.: um job de background checando uma flag de outro
/// tenant). Cacheia em Redis por 5 minutos (ver ADR-038); a ausência de registro é tratada
/// como desabilitada (fail-closed) — um módulo nunca "vaza" ligado por omissão.
/// </summary>
public sealed class FeatureFlagService(EfDbContext dbContext, ICacheService cacheService) : IFeatureFlagService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<bool> IsEnabledAsync(string key, Guid tenantId, CancellationToken cancellationToken = default) =>
        cacheService.GetOrSetAsync(
            BuildCacheKey(key, tenantId),
            async ct =>
            {
                var featureFlagKey = FeatureFlagKey.Create(key);
                if (featureFlagKey.IsFailure)
                {
                    return false;
                }

                var featureFlag = await dbContext.Set<FeatureFlag>()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Key == featureFlagKey.Value, ct);

                return featureFlag?.IsEnabled ?? false;
            },
            CacheDuration,
            cancellationToken);

    public Task InvalidateCacheAsync(string key, Guid tenantId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(BuildCacheKey(key, tenantId), cancellationToken);

    private static string BuildCacheKey(string key, Guid tenantId) => $"featureflag:{tenantId}:{key}";
}
