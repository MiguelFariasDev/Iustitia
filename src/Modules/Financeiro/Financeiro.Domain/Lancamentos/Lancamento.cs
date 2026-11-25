using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Financeiro.Domain.Lancamentos;

/// <summary>
/// Lançamento financeiro do escritório: honorário, conta a pagar ou conta a receber.
///
/// O VALOR é dinheiro: <c>decimal</c> e numeric(18,2) no banco, nunca double. Um lançamento
/// pago é histórico contábil — não se edita o valor nem se desfaz o pagamento; o caminho para
/// corrigir é excluir (soft delete, auditado) e lançar de novo.
/// </summary>
public sealed class Lancamento : AggregateRoot<LancamentoId>, IAuditable, ISoftDeletable, IHasTenant
{
    public const int DescricaoMaxLength = 200;
    public const int PessoaMaxLength = 200;
    public const int ProcessoMaxLength = 30;
    public const decimal ValorMaximo = 999_999_999_999m;

    /// <summary>Teto de parcelas (parcelado) e de meses (cobrança mensal) numa série.</summary>
    public const int MaxParcelas = 60;

    public Guid TenantId { get; private set; }

    public TipoLancamento Tipo { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Cliente (honorário, conta a receber) ou fornecedor (conta a pagar), em texto livre.</summary>
    public string? Pessoa { get; private set; }

    /// <summary>Número do processo a que o lançamento se refere, em texto livre (pode não ser CNJ).</summary>
    public string? Processo { get; private set; }

    public decimal Valor { get; private set; }

    public DateOnly Vencimento { get; private set; }

    public StatusLancamento Status { get; private set; }

    /// <summary>Quando foi pago; nulo enquanto pendente. Base do faturamento do mês e do fluxo de caixa.</summary>
    public DateTimeOffset? PagoEm { get; private set; }

    /// <summary>Avulso, parcela de um parcelamento ou mês de uma cobrança mensal.</summary>
    public RecorrenciaLancamento Recorrencia { get; private set; }

    /// <summary>Liga as parcelas (ou os meses) de uma mesma série; nulo no lançamento avulso.</summary>
    public Guid? SerieId { get; private set; }

    /// <summary>Posição na série (1 = primeira); nulo no lançamento avulso.</summary>
    public int? Parcela { get; private set; }

    /// <summary>Tamanho da série; nulo no lançamento avulso.</summary>
    public int? TotalParcelas { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Lancamento()
    {
        // EF Core.
    }

    private Lancamento(LancamentoId id, Guid tenantId, TipoLancamento tipo, string descricao, decimal valor, DateOnly vencimento)
        : base(id)
    {
        TenantId = tenantId;
        Tipo = tipo;
        Descricao = descricao;
        Valor = valor;
        Vencimento = vencimento;
        Status = StatusLancamento.Pendente;
    }

    /// <param name="agora">Só usado quando o lançamento já nasce pago (<paramref name="jaPago"/>).</param>
    public static Result<Lancamento> Create(
        Guid tenantId,
        TipoLancamento tipo,
        string? descricao,
        string? pessoa,
        string? processo,
        decimal valor,
        DateOnly vencimento,
        bool jaPago,
        DateTimeOffset agora)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Lancamento>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (!Enum.IsDefined(tipo))
        {
            return Result.Failure<Lancamento>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(Tipo)));
        }

        var descricaoNormalizada = descricao?.Trim();
        if (string.IsNullOrEmpty(descricaoNormalizada) || descricaoNormalizada.Length > DescricaoMaxLength)
        {
            return Result.Failure<Lancamento>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_DESCRIPTION_INVALID));
        }

        if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
        {
            return Result.Failure<Lancamento>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_AMOUNT_INVALID));
        }

        var lancamento = new Lancamento(LancamentoId.New(), tenantId, tipo, descricaoNormalizada, valor, vencimento)
        {
            Pessoa = Normalizar(pessoa, PessoaMaxLength),
            Processo = Normalizar(processo, ProcessoMaxLength),
        };

        if (jaPago)
        {
            lancamento.Status = StatusLancamento.Pago;
            lancamento.PagoEm = agora;
        }

        lancamento.RaiseDomainEvent(new LancamentoCriadoEvent(lancamento.Id.Value, tenantId, tipo, valor));

        return Result.Success(lancamento);
    }

    /// <summary>
    /// Série de lançamentos com vencimentos mensais a partir de <paramref name="primeiroVencimento"/>.
    /// <list type="bullet">
    /// <item><b>Parcelado</b>: <paramref name="valor"/> é o TOTAL, dividido em <paramref name="quantidade"/>
    /// parcelas; os centavos que sobram da divisão vão para a última, para a soma bater exatamente.</item>
    /// <item><b>Mensal</b>: <paramref name="valor"/> é o de CADA mês, repetido <paramref name="quantidade"/> vezes.</item>
    /// </list>
    /// Vencimento em dia que o mês não tem (31 em fevereiro) cai no último dia do mês. <paramref name="primeiraPaga"/>
    /// marca só a primeira como paga (entrada já recebida).
    /// </summary>
    public static Result<IReadOnlyList<Lancamento>> CriarSerie(
        Guid tenantId,
        TipoLancamento tipo,
        RecorrenciaLancamento recorrencia,
        int quantidade,
        string? descricao,
        string? pessoa,
        string? processo,
        decimal valor,
        DateOnly primeiroVencimento,
        bool primeiraPaga,
        DateTimeOffset agora)
    {
        if (recorrencia == RecorrenciaLancamento.Unica || !Enum.IsDefined(recorrencia))
        {
            return Result.Failure<IReadOnlyList<Lancamento>>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(Recorrencia)));
        }

        if (quantidade < 2 || quantidade > MaxParcelas)
        {
            return Result.Failure<IReadOnlyList<Lancamento>>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_INSTALLMENTS_INVALID));
        }

        if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
        {
            return Result.Failure<IReadOnlyList<Lancamento>>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_AMOUNT_INVALID));
        }

        var valorParcela = recorrencia == RecorrenciaLancamento.Parcelado
            ? decimal.Floor(valor / quantidade * 100m) / 100m
            : valor;
        if (valorParcela <= 0)
        {
            return Result.Failure<IReadOnlyList<Lancamento>>(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_AMOUNT_INVALID));
        }

        var serieId = Guid.NewGuid();
        var itens = new List<Lancamento>(quantidade);
        for (var numero = 1; numero <= quantidade; numero++)
        {
            var valorDesta = recorrencia == RecorrenciaLancamento.Parcelado && numero == quantidade
                ? valor - (valorParcela * (quantidade - 1))
                : valorParcela;

            var criado = Create(
                tenantId, tipo, descricao, pessoa, processo, valorDesta,
                primeiroVencimento.AddMonths(numero - 1), primeiraPaga && numero == 1, agora);
            if (criado.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Lancamento>>(criado.Error);
            }

            criado.Value.Recorrencia = recorrencia;
            criado.Value.SerieId = serieId;
            criado.Value.Parcela = numero;
            criado.Value.TotalParcelas = quantidade;
            itens.Add(criado.Value);
        }

        return Result.Success<IReadOnlyList<Lancamento>>(itens);
    }

    public Result MarcarComoPago(DateTimeOffset agora)
    {
        if (Status == StatusLancamento.Pago)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.FINANCE_ENTRY_ALREADY_PAID));
        }

        Status = StatusLancamento.Pago;
        PagoEm = agora;
        RaiseDomainEvent(new LancamentoPagoEvent(Id.Value, TenantId, Tipo, Valor));

        return Result.Success();
    }

    /// <summary>Pendente com vencimento anterior a <paramref name="hoje"/> está atrasado.</summary>
    public bool EstaAtrasado(DateOnly hoje) => Status == StatusLancamento.Pendente && Vencimento < hoje;

    /// <summary>Entrada de caixa (honorário e conta a receber) ou saída (conta a pagar).</summary>
    public bool EhSaida => Tipo == TipoLancamento.ContaPagar;

    private static string? Normalizar(string? valor, int max)
    {
        var texto = valor?.Trim();

        return string.IsNullOrEmpty(texto) ? null : texto.Length > max ? texto[..max] : texto;
    }
}

public sealed record LancamentoCriadoEvent(Guid LancamentoId, Guid TenantId, TipoLancamento Tipo, decimal Valor) : DomainEvent;

public sealed record LancamentoPagoEvent(Guid LancamentoId, Guid TenantId, TipoLancamento Tipo, decimal Valor) : DomainEvent;
