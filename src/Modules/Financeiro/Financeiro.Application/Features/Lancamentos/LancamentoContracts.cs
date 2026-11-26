using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Financeiro.Application.Features.Lancamentos;

/// <summary>
/// Novo lançamento. <c>Tipo</c> é "Honorario", "ContaPagar" ou "ContaReceber". <c>Pessoa</c> é o
/// cliente (honorário e conta a receber) ou o fornecedor (conta a pagar).
/// <para>
/// <c>Recorrencia</c>: "Unica" (padrão), "Parcelado" (<c>Valor</c> é o total, dividido em <c>Parcelas</c>)
/// ou "Mensal" (<c>Valor</c> é o de cada mês, repetido por <c>Parcelas</c> meses). Nas séries,
/// <c>Vencimento</c> é o da primeira e as demais vencem no mesmo dia dos meses seguintes; <c>JaPago</c>
/// marca só a primeira. A resposta é sempre a primeira parcela.
/// </para>
/// </summary>
[Audited("Created", "Lancamento")]
public sealed record CreateLancamentoCommand(
    string Tipo,
    string Descricao,
    string? Pessoa,
    string? Processo,
    decimal Valor,
    DateOnly Vencimento,
    bool JaPago = false,
    string? Recorrencia = null,
    int Parcelas = 1) : ICommand<LancamentoResponse>;

[Audited("Paid", "Lancamento", EntityIdProperty = nameof(LancamentoId))]
public sealed record MarcarLancamentoPagoCommand(Guid LancamentoId) : ICommand<LancamentoResponse>;

[Audited("Deleted", "Lancamento", EntityIdProperty = nameof(LancamentoId))]
public sealed record DeleteLancamentoCommand(Guid LancamentoId) : ICommand;

/// <param name="Status">"Pendente", "Pago" ou "Atrasado"; omitido = todos.</param>
/// <param name="Periodo">"mes" (vence neste mês) ou "proximos30"; omitido = qualquer data.</param>
public sealed record ListLancamentosQuery(
    string? Status = null,
    string? Periodo = null,
    int Page = 1,
    int PageSize = 100) : IQuery<ListLancamentosResponse>;

public sealed record GetResumoFinanceiroQuery : IQuery<ResumoFinanceiroResponse>;

public sealed record GetFluxoCaixaQuery(int Meses = 6) : IQuery<IReadOnlyList<FluxoCaixaMesItem>>;

public sealed record LancamentoResponse(
    Guid Id,
    string Tipo,
    string Descricao,
    string? Pessoa,
    string? Processo,
    decimal Valor,
    DateOnly Vencimento,
    string Status,
    DateTimeOffset? PagoEm,
    DateTimeOffset CreatedAt,
    string Recorrencia = "Unica",
    Guid? SerieId = null,
    int? Parcela = null,
    int? TotalParcelas = null);

public sealed record ListLancamentosResponse(
    IReadOnlyList<LancamentoResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ResumoFinanceiroResponse(
    decimal FaturamentoMes,
    decimal HonorariosAReceber,
    decimal ContasAPagar,
    decimal ContasAReceber,
    decimal Saldo);

public sealed record FluxoCaixaMesItem(int Ano, int Mes, decimal Entradas, decimal Saidas);
