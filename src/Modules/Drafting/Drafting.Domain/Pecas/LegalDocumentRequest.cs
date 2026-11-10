using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Pedido de minuta feito por um usuário, com tudo o que a geração produziu: texto, fontes
/// recuperadas, modelo, tempo, custo estimado e trilha de eventos. É a unidade de versionamento:
/// regerar cria uma NOVA solicitação com <see cref="Versao"/> + 1 apontando para a anterior
/// (<see cref="RequestOrigemId"/>); versões antigas nunca são sobrescritas.
///
/// Os textos de entrada chegam JÁ sanitizados (ver InputSanitizer na camada Application).
/// Durante a geração, <see cref="TextoGerado"/> guarda o texto PARCIAL (é o que o endpoint de
/// stream lê); só vale como minuta quando <see cref="Status"/> é Concluido.
/// </summary>
public sealed class LegalDocumentRequest : AggregateRoot<LegalDocumentRequestId>, IAuditable, IHasTenant
{
    public const int TipoMaxLength = 60;
    public const int AreaMaxLength = 60;
    public const int ModeloMaxLength = 100;
    public const int ErroMaxLength = 500;
    public const int EtapaMaxLength = 80;

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

    /// <summary>Texto curto da etapa atual, mostrado ao usuário ("Buscando fundamentos no acervo").</summary>
    public string? EtapaAtual { get; private set; }

    /// <summary>0–100, aproximado: serve de barra de progresso, não de medida.</summary>
    public int Progresso { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? IniciadoEm { get; private set; }

    public DateTimeOffset? ConcluidoEm { get; private set; }

    public DateTimeOffset? CanceladoEm { get; private set; }

    public string? TextoGerado { get; private set; }

    public string? ModeloUtilizado { get; private set; }

    public long? TempoGeracaoMs { get; private set; }

    /// <summary>Tokens que o modelo informou ter lido/escrito (quando o Ollama devolve a contagem).</summary>
    public int? TokensEntrada { get; private set; }

    public int? TokensSaida { get; private set; }

    /// <summary>
    /// Custo ESTIMADO só da geração (tempo de geração × preço/hora configurado). Não inclui o tempo
    /// de subida e de ociosidade da GPU, que é compartilhado entre os escritórios.
    /// </summary>
    public decimal? CustoEstimadoUsd { get; private set; }

    /// <summary>Motivo técnico curto da falha. Nunca é devolvido ao frontend.</summary>
    public string? Erro { get; private set; }

    /// <summary>Quantas vezes a geração foi tentada (conta só tentativas reais, não espera por GPU).</summary>
    public int Tentativas { get; private set; }

    /// <summary>Versão desta peça dentro da cadeia de regerações (1 = primeira).</summary>
    public int Versao { get; private set; } = 1;

    public LegalDocumentRequestId? RequestOrigemId { get; private set; }

    /// <summary>Versão do system prompt usada (ver SystemPrompt.Versao), para regressão e auditoria.</summary>
    public string? PromptVersao { get; private set; }

    /// <summary>Documentos que o usuário pediu para restringir a busca (ids dos documentos indexados).</summary>
    public string? DocumentosIdsJson { get; private set; }

    /// <summary>Avisos da verificação pós-geração (citações desconhecidas, seções ausentes…), em JSON.</summary>
    public string? AvisosJson { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public IReadOnlyCollection<RetrievedSource> Sources => _sources;

    public IReadOnlyCollection<LegalDocumentEvent> Events => _events;

    public bool IsTerminal => Status is LegalDocumentStatus.Concluido or LegalDocumentStatus.Falhou or LegalDocumentStatus.Cancelado;

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

        return Result.Success(new LegalDocumentRequest(LegalDocumentRequestId.New())
        {
            TenantId = tenantId,
            UsuarioId = usuarioId,
            TipoPeca = tipoPeca,
            Area = area,
            Fatos = fatos,
            // "pedidos" é opcional (recomendado): vazio vira texto explícito, para o modelo não inventar.
            Pedidos = pedidos,
            InstrucoesAdicionais = string.IsNullOrWhiteSpace(instrucoesAdicionais) ? null : instrucoesAdicionais,
            DocumentosIdsJson = documentosIdsJson,
            Status = LegalDocumentStatus.Pendente,
            EtapaAtual = "Na fila de processamento",
            Progresso = 0,
            CriadoEm = agora,
            Versao = versao,
            RequestOrigemId = requestOrigemId,
        });
    }

    /// <summary>Um worker reservou o item da fila: começa uma tentativa.</summary>
    public void Claim()
    {
        Status = LegalDocumentStatus.NaFila;
        EtapaAtual = "Preparando para processar";
        Progresso = 5;
        Tentativas++;
    }

    public void SetStage(LegalDocumentStatus status, string etapa, int progresso)
    {
        Status = status;
        EtapaAtual = Truncate(etapa, EtapaMaxLength);
        Progresso = Math.Clamp(progresso, 0, 100);
    }

    public void StartGeneration(string promptVersao, string modelo, DateTimeOffset agora)
    {
        PromptVersao = promptVersao;
        ModeloUtilizado = Truncate(modelo, ModeloMaxLength);
        IniciadoEm ??= agora;
    }

    public void SetPartial(string textoParcial, int progresso)
    {
        TextoGerado = textoParcial;
        Progresso = Math.Clamp(progresso, 0, 100);
    }

    public void AddUsage(int? tokensEntrada, int? tokensSaida)
    {
        TokensEntrada = tokensEntrada ?? TokensEntrada;
        TokensSaida = tokensSaida ?? TokensSaida;
    }

    public void Complete(
        string textoGerado, long tempoGeracaoMs, decimal? custoEstimadoUsd, string? avisosJson, DateTimeOffset agora)
    {
        Status = LegalDocumentStatus.Concluido;
        EtapaAtual = "Minuta pronta para revisão";
        Progresso = 100;
        TextoGerado = textoGerado;
        TempoGeracaoMs = tempoGeracaoMs;
        CustoEstimadoUsd = custoEstimadoUsd;
        AvisosJson = avisosJson;
        ConcluidoEm = agora;
        Erro = null;
    }

    /// <summary>Falha definitiva: guarda o motivo técnico e o que já tiver sido gerado (útil para diagnóstico).</summary>
    public void Fail(string erro, string? textoParcial, long tempoGeracaoMs, DateTimeOffset agora)
    {
        Status = LegalDocumentStatus.Falhou;
        EtapaAtual = "Não foi possível gerar a peça";
        Erro = Truncate(erro, ErroMaxLength);
        TextoGerado = string.IsNullOrEmpty(textoParcial) ? null : textoParcial;
        TempoGeracaoMs = tempoGeracaoMs;
        ConcluidoEm = agora;
    }

    /// <summary>Falha transitória com tentativas sobrando: volta para a fila, sem perder o motivo.</summary>
    public void Requeue(string erro)
    {
        Status = LegalDocumentStatus.Pendente;
        EtapaAtual = "Reagendada: nova tentativa em breve";
        Progresso = 0;
        TextoGerado = null;
        Erro = Truncate(erro, ErroMaxLength);
    }

    /// <summary>Volta a Pendente sem consumir tentativa: a infraestrutura de IA estava indisponível, não a geração.</summary>
    public void WaitForInfrastructure(string etapa)
    {
        Status = LegalDocumentStatus.Pendente;
        EtapaAtual = Truncate(etapa, EtapaMaxLength);
        Progresso = 0;
        if (Tentativas > 0)
        {
            Tentativas--;
        }
    }

    public Result Cancel(DateTimeOffset agora)
    {
        if (IsTerminal)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_CANNOT_CANCEL));
        }

        Status = LegalDocumentStatus.Cancelado;
        EtapaAtual = "Cancelada pelo usuário";
        CanceladoEm = agora;
        ConcluidoEm = agora;

        return Result.Success();
    }

    public RetrievedSource AddSource(
        string qdrantPointId,
        Guid? documentoOrigemId,
        string nomeArquivo,
        int? pagina,
        string trecho,
        double score,
        string? metadadosJson,
        bool utilizadaNaGeracao)
    {
        var source = new RetrievedSource(
            TenantId, Id, qdrantPointId, documentoOrigemId, nomeArquivo, pagina, trecho, score, metadadosJson, utilizadaNaGeracao);
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
