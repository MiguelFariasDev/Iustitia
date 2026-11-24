using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;

namespace Advocacia.Modules.Financeiro.Application.Abstractions;

/// <summary>Filtro de situação da listagem. "Atrasado" é derivado (pendente já vencido).</summary>
public enum FiltroStatusLancamento
{
    Todos,
    Pendente,
    Pago,
    Atrasado,
}

public interface ILancamentoRepository : IRepository<Lancamento, LancamentoId>
{
    /// <summary>Lançamentos do escritório por vencimento, com filtro de situação e janela de vencimento.</summary>
    Task<IReadOnlyList<Lancamento>> ListAsync(
        Guid tenantId,
        FiltroStatusLancamento status,
        DateOnly hoje,
        DateOnly? vencimentoDe,
        DateOnly? vencimentoAte,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid tenantId,
        FiltroStatusLancamento status,
        DateOnly hoje,
        DateOnly? vencimentoDe,
        DateOnly? vencimentoAte,
        CancellationToken cancellationToken = default);

    /// <summary>Totais agregados no banco — o resumo nunca depende de a lista caber numa página.</summary>
    Task<TotaisFinanceiros> TotaisAsync(
        Guid tenantId, DateTimeOffset inicioDoMes, DateTimeOffset inicioDoProximoMes, CancellationToken cancellationToken = default);

    /// <summary>Lançamentos PAGOS no intervalo (entradas e saídas), para o fluxo de caixa.</summary>
    Task<IReadOnlyList<PagamentoRealizado>> PagamentosAsync(
        Guid tenantId, DateTimeOffset desde, CancellationToken cancellationToken = default);
}

public sealed record TotaisFinanceiros(
    decimal FaturamentoMes,
    decimal HonorariosAReceber,
    decimal ContasAPagar,
    decimal ContasAReceber,
    decimal Recebido,
    decimal Pago);

public sealed record PagamentoRealizado(bool Saida, decimal Valor, DateTimeOffset PagoEm);
