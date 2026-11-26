using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;

namespace Advocacia.Modules.Legal.Application.Abstractions;

public interface INotificacaoClienteRepository : IRepository<NotificacaoCliente, NotificacaoClienteId>
{
    /// <summary>O rascunho pendente do processo, se houver (no máximo um — índice único parcial).</summary>
    Task<NotificacaoCliente?> GetPendenteDoProcessoAsync(Guid processoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacaoCliente>> ListAsync(
        StatusNotificacaoCliente? status, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(StatusNotificacaoCliente? status, CancellationToken cancellationToken = default);
}
