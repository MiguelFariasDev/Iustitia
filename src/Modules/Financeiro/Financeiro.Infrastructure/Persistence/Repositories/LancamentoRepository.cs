using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Financeiro.Application.Abstractions;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence.Repositories;

public sealed class LancamentoRepository(FinanceiroDbContext dbContext)
    : Repository<Lancamento, LancamentoId>(dbContext), ILancamentoRepository
{
    public async Task<IReadOnlyList<Lancamento>> ListAsync(
        Guid tenantId,
        FiltroStatusLancamento status,
        DateOnly hoje,
        DateOnly? vencimentoDe,
        DateOnly? vencimentoAte,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await Filtrar(tenantId, status, hoje, vencimentoDe, vencimentoAte)
            .OrderBy(lancamento => lancamento.Vencimento)
            .ThenBy(lancamento => lancamento.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        Guid tenantId,
        FiltroStatusLancamento status,
        DateOnly hoje,
        DateOnly? vencimentoDe,
        DateOnly? vencimentoAte,
        CancellationToken cancellationToken = default) =>
        Filtrar(tenantId, status, hoje, vencimentoDe, vencimentoAte).CountAsync(cancellationToken);

    public async Task<TotaisFinanceiros> TotaisAsync(
        Guid tenantId, DateTimeOffset inicioDoMes, DateTimeOffset inicioDoProximoMes, CancellationToken cancellationToken = default)
    {
        // Agregado no banco, uma única ida. Sum de conjunto vazio devolve 0 (cast para decimal? → ?? 0).
        var linhas = await DbSet
            .Where(lancamento => lancamento.TenantId == tenantId)
            .GroupBy(lancamento => new { lancamento.Tipo, lancamento.Status })
            .Select(grupo => new
            {
                grupo.Key.Tipo,
                grupo.Key.Status,
                Total = grupo.Sum(lancamento => lancamento.Valor),
            })
            .ToListAsync(cancellationToken);

        var faturamentoMes = await DbSet
            .Where(lancamento => lancamento.TenantId == tenantId
                && lancamento.Tipo == TipoLancamento.Honorario
                && lancamento.Status == StatusLancamento.Pago
                && lancamento.PagoEm >= inicioDoMes
                && lancamento.PagoEm < inicioDoProximoMes)
            .SumAsync(lancamento => (decimal?)lancamento.Valor, cancellationToken) ?? 0m;

        decimal Soma(Func<TipoLancamento, StatusLancamento, bool> filtro) =>
            linhas.Where(linha => filtro(linha.Tipo, linha.Status)).Sum(linha => linha.Total);

        return new TotaisFinanceiros(
            faturamentoMes,
            HonorariosAReceber: Soma((tipo, status) => tipo == TipoLancamento.Honorario && status != StatusLancamento.Pago),
            ContasAPagar: Soma((tipo, status) => tipo == TipoLancamento.ContaPagar && status != StatusLancamento.Pago),
            ContasAReceber: Soma((tipo, status) => tipo == TipoLancamento.ContaReceber && status != StatusLancamento.Pago),
            Recebido: Soma((tipo, status) => tipo != TipoLancamento.ContaPagar && status == StatusLancamento.Pago),
            Pago: Soma((tipo, status) => tipo == TipoLancamento.ContaPagar && status == StatusLancamento.Pago));
    }

    public async Task<IReadOnlyList<PagamentoRealizado>> PagamentosAsync(
        Guid tenantId, DateTimeOffset desde, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(lancamento => lancamento.TenantId == tenantId
                && lancamento.Status == StatusLancamento.Pago
                && lancamento.PagoEm >= desde)
            .Select(lancamento => new PagamentoRealizado(
                lancamento.Tipo == TipoLancamento.ContaPagar, lancamento.Valor, lancamento.PagoEm!.Value))
            .ToListAsync(cancellationToken);

    private IQueryable<Lancamento> Filtrar(
        Guid tenantId, FiltroStatusLancamento status, DateOnly hoje, DateOnly? de, DateOnly? ate)
    {
        var query = DbSet.Where(lancamento => lancamento.TenantId == tenantId);

        query = status switch
        {
            FiltroStatusLancamento.Pendente => query.Where(l => l.Status == StatusLancamento.Pendente && l.Vencimento >= hoje),
            FiltroStatusLancamento.Pago => query.Where(l => l.Status == StatusLancamento.Pago),
            FiltroStatusLancamento.Atrasado => query.Where(l => l.Status == StatusLancamento.Pendente && l.Vencimento < hoje),
            _ => query,
        };

        if (de is { } inicio)
        {
            query = query.Where(l => l.Vencimento >= inicio);
        }

        if (ate is { } fim)
        {
            query = query.Where(l => l.Vencimento <= fim);
        }

        return query;
    }
}
