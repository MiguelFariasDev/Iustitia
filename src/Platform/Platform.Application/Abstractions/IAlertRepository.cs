using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts;

namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Consultas de alertas. Todas são escopadas ao usuário: alerta é pessoal, e o filtro por
/// tenant sozinho deixaria um usuário ler os alertas de um colega do mesmo escritório.
/// Alertas descartados nunca aparecem.
/// </summary>
public interface IAlertRepository : IRepository<Alert, AlertId>
{
    Task<IReadOnlyList<Alert>> ListAsync(
        Guid tenantId, Guid userId, AlertFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid tenantId, Guid userId, AlertFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<AlertType, int>> CountUnreadByTypeAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<AlertType, int>> CountTotalByTypeAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Alert?> GetForUserAsync(Guid tenantId, Guid userId, AlertId id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid tenantId, Guid userId, AlertType type, Guid referenceId, CancellationToken cancellationToken = default);

    /// <summary>Marca todos os não lidos do usuário como lidos numa única instrução. Devolve quantos mudaram.</summary>
    Task<int> MarkAllAsReadAsync(Guid tenantId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <param name="Types">Um ou mais tipos (a aba "Outros" reúne tudo que não é publicação). Vazio = todos.</param>
public sealed record AlertFilter(
    IReadOnlyCollection<AlertType>? Types = null,
    bool? IsRead = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
