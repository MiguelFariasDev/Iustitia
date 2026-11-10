using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Pedido de minuta feito por um usuário, com tudo o que a geração produziu: texto, fontes
/// recuperadas, modelo, tempo e trilha de eventos. É a unidade de versionamento: regerar uma
/// peça cria uma NOVA solicitação com <see cref="Versao"/> + 1 apontando para a anterior
/// (<see cref="RequestOrigemId"/>); versões antigas nunca são sobrescritas.
///
/// Os textos de entrada chegam JÁ sanitizados (ver InputSanitizer na camada Application).
/// </summary>
public sealed class LegalDocumentRequest : AggregateRoot<LegalDocumentRequestId>, IAuditable, IHasTenant
{
    public const int TipoMaxLength = 60;
    public const int AreaMaxLength = 60;
    public const int ModeloMaxLength = 100;
    public const int ErroMaxLength = 500;

    private readonly List<RetrievedSource> _sources = [];
    private readonly List<LegalDocumentEvent> _events = [];

    public Guid TenantId { get; private set; }

    /// <summary>Usuário que pediu a peça (do JWT, nunca do corpo da requisição).</summary>
    public Guid UsuarioId { get; private set; }

    public string TipoPeca { get; private set; } = string.Empty;

    public string Area { get; private set; } = string.Empty;

    public string Fatos { get; private set; } = string.Empty;

    public string Pedidos { get; private set; } = string.Empty;

    public string? InstrucoesAdicionais { get; private set; }

    public LegalDocumentStatus Status { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? ConcluidoEm { get; private set; }

    public string? TextoGerado { get; private set; }

    public string? ModeloUtilizado { get; private set; }

    public long? TempoGeracaoMs { get; private set; }

    /// <summary>Motivo técnico curto da falha. Nunca é devolvido ao frontend.</summary>
    public string? Erro { get; private set; }

    /// <summary>Versão desta peça dentro da cadeia de regerações (1 = primeira).</summary>
    public int Versao { get; private set; } = 1;

    public LegalDocumentRequestId? RequestOrigemId { get; private set; }

    /// <summary>Versão do system prompt usada (ver SystemPrompt.Versao), para regressão e auditoria.</summary>
    public string? PromptVersao { get; private set; }

    /// <summary>Documentos que o usuário pediu para restringir a busca (ids do módulo Documents).</summary>
    public string? DocumentosIdsJson { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public IReadOnlyCollection<RetrievedSource> Sources => _sources;

    public IReadOnlyCollection<LegalDocumentEvent> Events => _events;

    private LegalDocumentRequest()
    {
        // EF Core.
    }

    private LegalDocumentRequest(LegalDocumentRequestId id)
        : base(id)
    {
    }

    public static Result<LegalDocumentRequest> Create(
        Guid tenantId,
        Guid usuarioId,
        string tipoPeca,
        string area,
        string fatos,
        string pedidos,
        string? instrucoesAdicionais,
        string? documentosIdsJson,
        DateTimeOffset agora,
        int versao = 1,
        LegalDocumentRequestId? requestOrigemId = null)
    {
        if (string.IsNullOrWhiteSpace(fatos))
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "fatos"));
        }

        if (string.IsNullOrWhiteSpace(pedidos))
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "pedidos"));
        }

        return Result.Success(new LegalDocumentRequest(LegalDocumentRequestId.New())
        {
            TenantId = tenantId,
            UsuarioId = usuarioId,
            TipoPeca = tipoPeca,
            Area = area,
            Fatos = fatos,
            Pedidos = pedidos,
            InstrucoesAdicionais = string.IsNullOrWhiteSpace(instrucoesAdicionais) ? null : instrucoesAdicionais,
            DocumentosIdsJson = documentosIdsJson,
            Status = LegalDocumentStatus.Pendente,
            CriadoEm = agora,
            Versao = versao,
            RequestOrigemId = requestOrigemId,
        });
    }

    public void StartProcessing(string promptVersao, string modelo)
    {
        Status = LegalDocumentStatus.Processando;
        PromptVersao = promptVersao;
        ModeloUtilizado = Truncate(modelo, ModeloMaxLength);
    }

    public void Complete(string textoGerado, long tempoGeracaoMs, DateTimeOffset agora)
    {
        Status = LegalDocumentStatus.Concluido;
        TextoGerado = textoGerado;
        TempoGeracaoMs = tempoGeracaoMs;
        ConcluidoEm = agora;
        Erro = null;
    }

    /// <summary>Falha: guarda o motivo técnico e o que já tiver sido gerado (útil para diagnóstico).</summary>
    public void Fail(string erro, string? textoParcial, long tempoGeracaoMs, DateTimeOffset agora)
    {
        Status = LegalDocumentStatus.Falhou;
        Erro = Truncate(erro, ErroMaxLength);
        TextoGerado = string.IsNullOrEmpty(textoParcial) ? null : textoParcial;
        TempoGeracaoMs = tempoGeracaoMs;
        ConcluidoEm = agora;
    }

    public RetrievedSource AddSource(
        string qdrantPointId,
        Guid? documentoOrigemId,
        string nomeArquivo,
        int? pagina,
        string trecho,
        double score,
        string? metadadosJson,
        bool citada)
    {
        var source = new RetrievedSource(
            TenantId, Id, qdrantPointId, documentoOrigemId, nomeArquivo, pagina, trecho, score, metadadosJson, citada);
        _sources.Add(source);

        return source;
    }

    public LegalDocumentEvent AddEvent(string tipo, DateTimeOffset ocorridoEm, string? detalheJson = null)
    {
        var evento = new LegalDocumentEvent(TenantId, Id, tipo, ocorridoEm, detalheJson);
        _events.Add(evento);

        return evento;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
