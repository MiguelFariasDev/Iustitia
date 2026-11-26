using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.Movimentacoes;

internal sealed class MovimentacoesEmMemoria : IProcessoMovimentacaoRepository
{
    internal List<ProcessoMovimentacao> Itens { get; } = [];

    public void Add(ProcessoMovimentacao entity) => Itens.Add(entity);

    public void Update(ProcessoMovimentacao entity)
    {
    }

    public void Remove(ProcessoMovimentacao entity) => Itens.Remove(entity);

    public Task<ProcessoMovimentacao?> GetByIdAsync(ProcessoMovimentacaoId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(m => m.Id == id));

    public Task<IReadOnlySet<string>> ExternalIdsExistentesAsync(
        Guid processoId, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(
            Itens.Where(m => m.ProcessoId == processoId && externalIds.Contains(m.ExternalId)).Select(m => m.ExternalId).ToHashSet());

    public Task<CapturaExistente?> UltimaCapturaAsync(Guid processoId, CancellationToken cancellationToken = default)
    {
        var doProcesso = Itens.Where(m => m.ProcessoId == processoId).ToList();
        if (doProcesso.Count == 0)
        {
            return Task.FromResult<CapturaExistente?>(null);
        }

        var fonte = doProcesso.OrderByDescending(m => m.CapturadaEm).First().Fonte;
        return Task.FromResult<CapturaExistente?>(
            new CapturaExistente(fonte, doProcesso.Where(m => m.Fonte == fonte).Max(m => m.DataHora)));
    }

    public Task<IReadOnlyList<ProcessoMovimentacao>> ListAsync(Guid processoId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessoMovimentacao>>(Itens.Where(m => m.ProcessoId == processoId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(Guid processoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.Count(m => m.ProcessoId == processoId));

    public Task<IReadOnlyList<ProcessoMovimentacao>> ListCapturadasDesdeAsync(
        Guid processoId, DateTimeOffset capturadasDesde, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessoMovimentacao>>(
            Itens.Where(m => m.ProcessoId == processoId && m.CapturadaEm >= capturadasDesde).ToList());

    public Task<ProcessoMovimentacao?> FirstOrDefaultAsync(ISpecification<ProcessoMovimentacao> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProcessoMovimentacao>> ListAsync(ISpecification<ProcessoMovimentacao> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(ISpecification<ProcessoMovimentacao> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class AvisosEmMemoria : INotificacaoClienteRepository
{
    internal List<NotificacaoCliente> Itens { get; } = [];

    public void Add(NotificacaoCliente entity) => Itens.Add(entity);

    public void Update(NotificacaoCliente entity)
    {
    }

    public void Remove(NotificacaoCliente entity) => Itens.Remove(entity);

    public Task<NotificacaoCliente?> GetByIdAsync(NotificacaoClienteId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(n => n.Id == id));

    public Task<NotificacaoCliente?> GetPendenteDoProcessoAsync(Guid processoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(n => n.ProcessoId == processoId && n.Pendente));

    public Task<IReadOnlyList<NotificacaoCliente>> ListAsync(
        StatusNotificacaoCliente? status, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificacaoCliente>>(Itens.Where(n => status is null || n.Status == status).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(StatusNotificacaoCliente? status, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.Count(n => status is null || n.Status == status));

    public Task<NotificacaoCliente?> FirstOrDefaultAsync(ISpecification<NotificacaoCliente> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<NotificacaoCliente>> ListAsync(ISpecification<NotificacaoCliente> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(ISpecification<NotificacaoCliente> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
