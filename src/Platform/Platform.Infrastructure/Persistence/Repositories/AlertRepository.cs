using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// Toda consulta repete tenant E usuário no Where, além do filtro global e do RLS: alerta é
/// pessoal, e o isolamento por tenant sozinho deixaria um colega ler os alertas do outro.
/// </summary>
public sealed class AlertRepository(PlatformDbContext dbContext)
    : Repository<Alert, AlertId>(dbContext), IAlertRepository
{
    public async Task<IReadOnlyList<Alert>> ListAsync(
        Guid tenantId, Guid userId, AlertFilter filter, int skip, int take, CancellationToken cancellationToken = default) =>
        await Query(tenantId, userId, filter)
            .OrderByDescending(alert => alert.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        Guid tenantId, Guid userId, AlertFilter filter, CancellationToken cancellationToken = default) =>
        Query(tenantId, userId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<AlertType, int>> CountUnreadByTypeAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await Query(tenantId, userId, new AlertFilter(IsRead: false))
            .GroupBy(alert => alert.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Type, row => row.Count);
    }

    public async Task<IReadOnlyDictionary<AlertType, int>> CountTotalByTypeAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await Query(tenantId, userId, new AlertFilter())
            .GroupBy(alert => alert.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Type, row => row.Count);
    }

    public Task<Alert?> GetForUserAsync(
        Guid tenantId, Guid userId, AlertId id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            alert => alert.Id == id && alert.TenantId == tenantId && alert.UserId == userId && alert.DismissedAt == null,
            cancellationToken);

    public Task<bool> ExistsAsync(
        Guid tenantId, Guid userId, AlertType type, Guid referenceId, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(
            alert => alert.TenantId == tenantId && alert.UserId == userId && alert.Type == type
                && alert.ReferenceId == referenceId,
            cancellationToken);

    public Task<int> MarkAllAsReadAsync(
        Guid tenantId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(alert => alert.TenantId == tenantId && alert.UserId == userId && !alert.IsRead && alert.DismissedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(alert => alert.IsRead, true).SetProperty(alert => alert.ReadAt, now),
                cancellationToken);

    private IQueryable<Alert> Query(Guid tenantId, Guid userId, AlertFilter filter)
    {
        var query = DbSet.Where(alert => alert.TenantId == tenantId && alert.UserId == userId && alert.DismissedAt == null);

        if (filter.Types is { Count: > 0 } types)
        {
            query = query.Where(alert => types.Contains(alert.Type));
        }

        if (filter.IsRead is { } isRead)
        {
            query = query.Where(alert => alert.IsRead == isRead);
        }

        if (filter.From is { } from)
        {
            query = query.Where(alert => alert.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(alert => alert.CreatedAt <= to);
        }

        return query;
    }
}
