using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Movimentacoes;

/// <summary>
/// Andamento de um processo capturado no tribunal (ADR-075). Imutável depois de gravado: o
/// tribunal não "edita" andamento, e um andamento diferente tem outro <see cref="ExternalId"/>.
///
/// A deduplicação é pelo índice único (tenant, processo, <see cref="ExternalId"/>): reconsultar a
/// mesma janela não duplica nada. O id vem da fonte — o SHA-256 estável do juscraper, ou
/// "datajud:" + hash para o DataJud. Ids de fontes diferentes nunca coincidem, por isso um processo
/// fica numa fonte só (ver <see cref="Fonte"/>).
///
/// O texto (<see cref="Nome"/>, <see cref="Complemento"/>) é conteúdo do processo: fica no banco,
/// sob RLS, e NUNCA vai para log, métrica ou alerta.
/// </summary>
public sealed class ProcessoMovimentacao : AggregateRoot<ProcessoMovimentacaoId>, IHasTenant, IAuditable
{
    public const int ExternalIdMaxLength = 80;
    public const int FonteMaxLength = 20;
    public const int NomeMaxLength = 500;
    public const int ComplementoMaxLength = 4000;
    public const int InstanciaMaxLength = 10;

    public Guid TenantId { get; private set; }

    public Guid ProcessoId { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>"juscraper" ou "datajud".</summary>
    public string Fonte { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public string? Complemento { get; private set; }

    /// <summary>Quando ocorreu. No e-SAJ só a data (hora 00:00 de Brasília); no PJe, data e hora.</summary>
    public DateTimeOffset? DataHora { get; private set; }

    /// <summary>G1, G2… quando a fonte separa as instâncias.</summary>
    public string? Instancia { get; private set; }

    public bool PossuiDocumento { get; private set; }

    public DateTimeOffset CapturadaEm { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    private ProcessoMovimentacao()
    {
        // EF Core.
    }

    private ProcessoMovimentacao(ProcessoMovimentacaoId id)
        : base(id)
    {
    }

    public static Result<ProcessoMovimentacao> Create(
        Guid tenantId,
        Guid processoId,
        string? externalId,
        string fonte,
        string? nome,
        string? complemento,
        DateTimeOffset? dataHora,
        string? instancia,
        bool possuiDocumento,
        DateTimeOffset capturadaEm)
    {
        if (tenantId == Guid.Empty || processoId == Guid.Empty)
        {
            return Result.Failure<ProcessoMovimentacao>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(ProcessoId)));
        }

        // Andamento sem id não é gravado: sem ele não há como saber, na noite seguinte, se já foi visto.
        if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > ExternalIdMaxLength)
        {
            return Result.Failure<ProcessoMovimentacao>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(ExternalId)));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure<ProcessoMovimentacao>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(Nome)));
        }

        return Result.Success(new ProcessoMovimentacao(ProcessoMovimentacaoId.New())
        {
            TenantId = tenantId,
            ProcessoId = processoId,
            ExternalId = externalId,
            Fonte = Cortar(fonte, FonteMaxLength)!,
            Nome = Cortar(nome, NomeMaxLength)!,
            Complemento = string.IsNullOrWhiteSpace(complemento) ? null : Cortar(complemento, ComplementoMaxLength),
            DataHora = dataHora,
            Instancia = string.IsNullOrWhiteSpace(instancia) ? null : Cortar(instancia, InstanciaMaxLength),
            PossuiDocumento = possuiDocumento,
            CapturadaEm = capturadaEm,
        });
    }

    private static string? Cortar(string? texto, int max)
    {
        var limpo = texto?.Trim();

        return limpo is null || limpo.Length <= max ? limpo : limpo[..max];
    }
}
