namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Checagem de feature flag em runtime, por tenant (ver ADR-038) — usada para decidir se
/// uma funcionalidade está liberada para um escritório específico. Diferente de
/// IRepository{FeatureFlag,FeatureFlagId} (usado pelos handlers de CRUD administrativo,
/// ver Features/FeatureFlags): esta interface é otimizada para leitura frequente (cache),
/// não para gestão da flag.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Falta de registro para o par (tenantId, key) é tratada como desabilitada (fail-closed).</summary>
    Task<bool> IsEnabledAsync(string key, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Chamado após um update administrativo da flag, para não servir um valor de cache desatualizado.</summary>
    Task InvalidateCacheAsync(string key, Guid tenantId, CancellationToken cancellationToken = default);
}
