using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

public sealed class NotificacaoClienteRepository(LegalDbContext dbContext)
    : Repository<NotificacaoCliente, NotificacaoClienteId>(dbContext), INotificacaoClienteRepository
{
    public Task<NotificacaoCliente?> GetPendenteDoProcessoAsync(Guid processoId, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            n => n.ProcessoId == processoId && n.Status == StatusNotificacaoCliente.PendenteRevisao, cancellationToken);

    public async Task<IReadOnlyList<NotificacaoCliente>> ListAsync(
        StatusNotificacaoCliente? status, int skip, int take, CancellationToken cancellationToken = default) =>
        await Filtrar(status)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(StatusNotificacaoCliente? status, CancellationToken cancellationToken = default) =>
        Filtrar(status).CountAsync(cancellationToken);

    private IQueryable<NotificacaoCliente> Filtrar(StatusNotificacaoCliente? status) =>
        status is null ? DbSet : DbSet.Where(n => n.Status == status);
}
