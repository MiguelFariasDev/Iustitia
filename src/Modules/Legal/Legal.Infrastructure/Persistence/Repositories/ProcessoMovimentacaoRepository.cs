using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

/// <summary>Tenant vem do filtro global do BaseDbContext e da RLS — nenhum método o recebe.</summary>
public sealed class ProcessoMovimentacaoRepository(LegalDbContext dbContext)
    : Repository<ProcessoMovimentacao, ProcessoMovimentacaoId>(dbContext), IProcessoMovimentacaoRepository
{
    public async Task<IReadOnlySet<string>> ExternalIdsExistentesAsync(
        Guid processoId, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default)
    {
        if (externalIds.Count == 0)
        {
            return new HashSet<string>();
        }

        var existentes = await DbSet
            .Where(m => m.ProcessoId == processoId && externalIds.Contains(m.ExternalId))
            .Select(m => m.ExternalId)
            .ToListAsync(cancellationToken);

        return existentes.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<CapturaExistente?> UltimaCapturaAsync(Guid processoId, CancellationToken cancellationToken = default)
    {
        // A fonte do processo é a do andamento gravado por último: depois de uma troca explícita, é a nova.
        var fonte = await DbSet
            .Where(m => m.ProcessoId == processoId)
            .OrderByDescending(m => m.CapturadaEm)
            .Select(m => m.Fonte)
            .FirstOrDefaultAsync(cancellationToken);

        if (fonte is null)
        {
            return null;
        }

        var ultimo = await DbSet
            .Where(m => m.ProcessoId == processoId && m.Fonte == fonte)
            .MaxAsync(m => m.DataHora, cancellationToken);

        return new CapturaExistente(fonte, ultimo);
    }

    public async Task<IReadOnlyList<ProcessoMovimentacao>> ListAsync(
        Guid processoId, int skip, int take, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(m => m.ProcessoId == processoId)
            .OrderByDescending(m => m.DataHora)
            .ThenByDescending(m => m.CapturadaEm)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(Guid processoId, CancellationToken cancellationToken = default) =>
        DbSet.CountAsync(m => m.ProcessoId == processoId, cancellationToken);

    public async Task<IReadOnlyList<ProcessoMovimentacao>> ListCapturadasDesdeAsync(
        Guid processoId, DateTimeOffset capturadasDesde, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(m => m.ProcessoId == processoId && m.CapturadaEm >= capturadasDesde)
            .ToListAsync(cancellationToken);
}
