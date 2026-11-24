using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Financeiro.Application.Abstractions;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;

namespace Advocacia.Modules.Financeiro.Application.Features.Lancamentos;

public sealed class CreateLancamentoHandler(
    ILancamentoRepository repository,
    IFinanceiroUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<CreateLancamentoCommand, LancamentoResponse>
{
    public async Task<Result<LancamentoResponse>> Handle(CreateLancamentoCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<LancamentoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<TipoLancamento>(request.Tipo, ignoreCase: true, out var tipo))
        {
            return Result.Failure<LancamentoResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Tipo)));
        }

        var agora = clock.UtcNow;
        var lancamento = Lancamento.Create(
            tenantId, tipo, request.Descricao, request.Pessoa, request.Processo, request.Valor, request.Vencimento, request.JaPago, agora);
        if (lancamento.IsFailure)
        {
            return Result.Failure<LancamentoResponse>(lancamento.Error);
        }

        repository.Add(lancamento.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(lancamento.Value.ToResponse(RelogioBrasil.Hoje(agora)));
    }
}

public sealed class MarcarLancamentoPagoHandler(
    ILancamentoRepository repository,
    IFinanceiroUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<MarcarLancamentoPagoCommand, LancamentoResponse>
{
    public async Task<Result<LancamentoResponse>> Handle(MarcarLancamentoPagoCommand request, CancellationToken cancellationToken)
    {
        var lancamento = await repository.GetByIdAsync(LancamentoId.From(request.LancamentoId), cancellationToken);
        if (lancamento is null)
        {
            return Result.Failure<LancamentoResponse>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_NOT_FOUND));
        }

        var agora = clock.UtcNow;
        var resultado = lancamento.MarcarComoPago(agora);
        if (resultado.IsFailure)
        {
            return Result.Failure<LancamentoResponse>(resultado.Error);
        }

        repository.Update(lancamento);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(lancamento.ToResponse(RelogioBrasil.Hoje(agora)));
    }
}

/// <summary>Exclusão lógica (auditada). O lançamento some das listas e dos totais, mas o registro permanece.</summary>
public sealed class DeleteLancamentoHandler(
    ILancamentoRepository repository,
    IFinanceiroUnitOfWork unitOfWork) : ICommandHandler<DeleteLancamentoCommand>
{
    public async Task<Result> Handle(DeleteLancamentoCommand request, CancellationToken cancellationToken)
    {
        var lancamento = await repository.GetByIdAsync(LancamentoId.From(request.LancamentoId), cancellationToken);
        if (lancamento is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_NOT_FOUND));
        }

        repository.Remove(lancamento);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ListLancamentosHandler(
    ILancamentoRepository repository,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<ListLancamentosQuery, ListLancamentosResponse>
{
    public async Task<Result<ListLancamentosResponse>> Handle(ListLancamentosQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListLancamentosResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var status = FiltroStatusLancamento.Todos;
        if (!string.IsNullOrWhiteSpace(request.Status)
            && !Enum.TryParse(request.Status, ignoreCase: true, out status))
        {
            return Result.Failure<ListLancamentosResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
        }

        var hoje = RelogioBrasil.Hoje(clock.UtcNow);
        DateOnly? de = null;
        DateOnly? ate = null;
        switch (request.Periodo)
        {
            case "mes":
                de = new DateOnly(hoje.Year, hoje.Month, 1);
                ate = de.Value.AddMonths(1).AddDays(-1);
                break;
            case "proximos30":
                de = hoje;
                ate = hoje.AddDays(30);
                break;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 100 : request.PageSize;

        var itens = await repository.ListAsync(tenantId, status, hoje, de, ate, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await repository.CountAsync(tenantId, status, hoje, de, ate, cancellationToken);

        return Result.Success(new ListLancamentosResponse(
            itens.Select(item => item.ToResponse(hoje)).ToList(),
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize)));
    }
}

/// <summary>Os totais vêm do banco: o resumo é exato mesmo quando a lista não cabe numa página.</summary>
public sealed class GetResumoFinanceiroHandler(
    ILancamentoRepository repository,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetResumoFinanceiroQuery, ResumoFinanceiroResponse>
{
    public async Task<Result<ResumoFinanceiroResponse>> Handle(GetResumoFinanceiroQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ResumoFinanceiroResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var inicio = RelogioBrasil.InicioDoMes(clock.UtcNow);
        var totais = await repository.TotaisAsync(tenantId, inicio, RelogioBrasil.AddMeses(inicio, 1), cancellationToken);

        return Result.Success(new ResumoFinanceiroResponse(
            totais.FaturamentoMes,
            totais.HonorariosAReceber,
            totais.ContasAPagar,
            totais.ContasAReceber,
            totais.Recebido - totais.Pago));
    }
}

public sealed class GetFluxoCaixaHandler(
    ILancamentoRepository repository,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetFluxoCaixaQuery, IReadOnlyList<FluxoCaixaMesItem>>
{
    public async Task<Result<IReadOnlyList<FluxoCaixaMesItem>>> Handle(GetFluxoCaixaQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<FluxoCaixaMesItem>>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var meses = request.Meses is < 1 or > 24 ? 6 : request.Meses;
        var mesAtual = RelogioBrasil.InicioDoMes(clock.UtcNow);
        var primeiro = RelogioBrasil.AddMeses(mesAtual, -(meses - 1));

        var pagamentos = await repository.PagamentosAsync(tenantId, primeiro, cancellationToken);

        // Agrupa no horário de Brasília, na mesma régua do resumo.
        var porMes = pagamentos
            .Select(p => (Local: RelogioBrasil.EmBrasilia(p.PagoEm), p.Saida, p.Valor))
            .GroupBy(p => (p.Local.Year, p.Local.Month))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (Entradas: grupo.Where(p => !p.Saida).Sum(p => p.Valor), Saidas: grupo.Where(p => p.Saida).Sum(p => p.Valor)));

        IReadOnlyList<FluxoCaixaMesItem> serie = Enumerable.Range(0, meses)
            .Select(indice =>
            {
                var mes = RelogioBrasil.AddMeses(primeiro, indice);
                porMes.TryGetValue((mes.Year, mes.Month), out var soma);

                return new FluxoCaixaMesItem(mes.Year, mes.Month, soma.Entradas, soma.Saidas);
            })
            .ToList();

        return Result.Success(serie);
    }
}
