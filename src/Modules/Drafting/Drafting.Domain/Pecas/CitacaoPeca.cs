using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>Resultado da conferência automática de uma citação (ou de um fato) da minuta.</summary>
public enum CitacaoStatus
{
    Validada = 0,
    PendenteDeVerificacao = 1,
    PossivelAlucinacao = 2,
    ConflitoDeVersao = 3,
    NormaRevogada = 4,
}

/// <summary>O que a pessoa que revisou decidiu.</summary>
public enum DecisaoCitacao
{
    Pendente = 0,

    /// <summary>Conferida na fonte oficial: está correta como está.</summary>
    Confirmada = 1,

    /// <summary>Estava errada e foi corrigida no texto final.</summary>
    Corrigida = 2,

    /// <summary>Não procede: não pode constar do texto final.</summary>
    Rejeitada = 3,
}

/// <summary>
/// Citação da minuta com o veredito da conferência automática e a decisão humana. É o que trava (ou
/// libera) a exportação final: item duvidoso sem decisão = exportação bloqueada.
/// </summary>
public sealed class CitacaoPeca : Entity<Guid>, IHasTenant
{
    public const int TextoMaxLength = 600;
    public const int DetalheMaxLength = 600;
    public const int JustificativaMaxLength = 1000;

    public Guid TenantId { get; private set; }

    public LegalDocumentRequestId LegalDocumentRequestId { get; private set; }

    public int Ordem { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public CitacaoStatus Status { get; private set; }

    public string Detalhe { get; private set; } = string.Empty;

    /// <summary>Trecho recuperado que sustenta a citação (id do ponto), quando há.</summary>
    public string? PontoId { get; private set; }

    public string? FonteUrl { get; private set; }

    public string? Urn { get; private set; }

    public DecisaoCitacao Decisao { get; private set; }

    public string? Justificativa { get; private set; }

    public Guid? DecididoPor { get; private set; }

    public DateTimeOffset? DecididoEm { get; private set; }

    /// <summary>Só estes dois status travam a exportação; os demais avisam.</summary>
    public bool Bloqueante => Status is CitacaoStatus.PossivelAlucinacao or CitacaoStatus.PendenteDeVerificacao;

    public bool Resolvida => !Bloqueante || Decisao != DecisaoCitacao.Pendente;

    private CitacaoPeca()
    {
        // EF Core.
    }

    internal CitacaoPeca(
        Guid tenantId, LegalDocumentRequestId requestId, int ordem, string texto, CitacaoStatus status, string detalhe,
        string? pontoId, string? fonteUrl, string? urn)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        LegalDocumentRequestId = requestId;
        Ordem = ordem;
        Texto = Cortar(texto, TextoMaxLength);
        Status = status;
        Detalhe = Cortar(detalhe, DetalheMaxLength);
        PontoId = pontoId;
        FonteUrl = fonteUrl is { Length: > 1000 } ? null : fonteUrl;
        Urn = urn is { Length: > 300 } ? null : urn;
    }

    public void Decidir(DecisaoCitacao decisao, string justificativa, Guid usuarioId, DateTimeOffset agora)
    {
        Decisao = decisao;
        Justificativa = Cortar(justificativa, JustificativaMaxLength);
        DecididoPor = usuarioId;
        DecididoEm = agora;
    }

    private static string Cortar(string valor, int max) => valor.Length <= max ? valor : valor[..max];
}
