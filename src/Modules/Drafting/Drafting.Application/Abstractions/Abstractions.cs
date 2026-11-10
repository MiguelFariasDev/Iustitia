using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Gpu;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Abstractions;

/// <summary>Falha do serviço de IA (rede, HTTP, resposta inválida). A mensagem é técnica e NÃO vai ao usuário.</summary>
public sealed class LlmServiceException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Falha do banco vetorial (rede, gRPC). Tratada como transitória: a geração é repetida.</summary>
public sealed class VectorStoreException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record LlmMessage(string Role, string Content);

public sealed record LlmChatRequest(IReadOnlyList<LlmMessage> Messages);

/// <param name="Content">Texto novo deste pedaço (vazio no pedaço final).</param>
public sealed record LlmChunk(string Content, bool Done, int? PromptTokens, int? OutputTokens);

/// <summary>Geração de texto. Hoje o Ollama; a interface existe para trocar o modelo ou o provedor.</summary>
public interface ILlmService
{
    /// <summary>Nome do modelo configurado (gravado na solicitação; nunca devolvido ao frontend).</summary>
    string ModelName { get; }

    IAsyncEnumerable<LlmChunk> ChatStreamAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Embeddings (BGE-M3, 1024 dimensões, via Ollama).</summary>
public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}

/// <summary>Saúde do servidor de modelos: <c>GET /api/tags</c> no Ollama.</summary>
public interface ILlmHealth
{
    Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default);

    /// <summary>Nomes dos modelos instalados no servidor (ex.: "qwen3:8b", "bge-m3:latest").</summary>
    Task<IReadOnlyList<string>> ListarModelosAsync(CancellationToken cancellationToken = default);
}

/// <summary>O Ollama como um todo (geração + embeddings + saúde): a fachada que a especificação chama de IOllamaService.</summary>
public interface IOllamaService : ILlmService, IEmbeddingService, ILlmHealth;

/// <summary>Endereço ATUAL do Ollama. Fixo (configuração) ou descoberto pela Vast.ai a cada subida da instância.</summary>
public interface IOllamaEndpoint
{
    Uri BaseAddress { get; }

    void Set(Uri baseAddress);
}

/// <summary>Banco vetorial (Qdrant hoje). Toda busca e toda exclusão são escopadas por tenant.</summary>
public interface IVectorStore
{
    Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchQuery query, CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);

    Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default);

    Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default);
}

public interface IRagService
{
    Task<RagContext> RetrieveAsync(RagQuery query, CancellationToken cancellationToken = default);
}

public sealed record PromptBundle(string System, string User, string Versao);

public interface IPromptBuilder
{
    PromptBundle Build(PecaInput input, IReadOnlyList<RetrievedChunk> chunks);
}

// ---------------------------------------------------------------- persistência

/// <summary>Leitura barata do estado de uma solicitação (sem rastrear a entidade).</summary>
public sealed record PecaSnapshot(
    Guid Id,
    Guid UsuarioId,
    LegalDocumentStatus Status,
    string? Etapa,
    int Progresso,
    string? Texto,
    int Versao,
    string? Erro,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidoEm);

public interface ILegalDocumentRepository : IRepository<LegalDocumentRequest, LegalDocumentRequestId>
{
    /// <summary>
    /// Marca um filho NOVO como Added de forma explícita. Sem isto, o EF trata um filho com chave já
    /// preenchida, descoberto na coleção de um pai que já existe, como "existente" e gera UPDATE
    /// (DbUpdateConcurrencyException) em vez de INSERT.
    /// </summary>
    void AddChild(RetrievedSource source);

    void AddChild(LegalDocumentEvent evento);

    Task<LegalDocumentRequest?> GetWithSourcesAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default);

    Task<PecaSnapshot?> GetSnapshotAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default);

    /// <param name="usuarioId">Nulo = todas do escritório (quem pode ver tudo).</param>
    Task<IReadOnlyList<LegalDocumentRequest>> ListRecentAsync(
        Guid? usuarioId, int skip, int take, CancellationToken cancellationToken = default);
}

public interface IIndexedDocumentRepository : IRepository<IndexedDocument, IndexedDocumentId>
{
    Task<IndexedDocument?> FindByHashAsync(string hashConteudo, CancellationToken cancellationToken = default);

    /// <summary>Documentos do escritório + os privados do próprio usuário. Privado de outro usuário nunca aparece.</summary>
    Task<IReadOnlyList<IndexedDocument>> ListVisibleAsync(
        Guid usuarioId, int skip, int take, CancellationToken cancellationToken = default);
}

public interface IGpuInstanceLogRepository
{
    void Add(GpuInstanceLog log);

    Task SaveAsync(CancellationToken cancellationToken = default);
}

public interface IDraftingUnitOfWork : IUnitOfWork
{
}

public interface ILegalDocumentService
{
    /// <summary>Cria a solicitação (Pendente) e o item da fila na MESMA transação.</summary>
    Task<Result<LegalDocumentRequest>> CreateAndEnqueueAsync(PecaInput input, CancellationToken cancellationToken = default);

    Task SetStageAsync(
        LegalDocumentRequest request, LegalDocumentStatus status, string etapa, int progresso,
        CancellationToken cancellationToken = default);

    Task WaitForInfrastructureAsync(LegalDocumentRequest request, string etapa, CancellationToken cancellationToken = default);

    Task ClaimAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default);

    Task StartGenerationAsync(LegalDocumentRequest request, string promptVersao, string modelo, CancellationToken cancellationToken = default);

    Task SavePartialAsync(LegalDocumentRequest request, string textoParcial, int progresso, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        LegalDocumentRequest request,
        string texto,
        long tempoMs,
        int? tokensEntrada,
        int? tokensSaida,
        IReadOnlyList<(RetrievedChunk Chunk, bool Utilizada)> fontes,
        IReadOnlyList<string> avisos,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        LegalDocumentRequest request, string erro, string? textoParcial, long tempoMs,
        IReadOnlyList<(RetrievedChunk Chunk, bool Utilizada)> fontes, CancellationToken cancellationToken = default);

    Task RequeueAsync(LegalDocumentRequest request, string erro, CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Trilha de auditoria e log do módulo: grava LegalDocumentEvent e emite log estruturado.
/// SÓ metadados (ids, contagens, tempos) — nunca fatos, pedidos, trechos ou a minuta.
/// </summary>
public interface IDraftingAuditService
{
    Task RecordAsync(
        LegalDocumentRequest request, string tipo, object? detalhe = null, CancellationToken cancellationToken = default);
}

public interface INotificacaoService
{
    /// <summary>Avisa o dono da peça (alerta in-app). Falha de notificação NUNCA derruba a geração.</summary>
    Task NotificarConclusaoAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default);

    Task NotificarFalhaAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Problema de infraestrutura (GPU) que precisa de gente: vira log de erro para o alarme da operação.</summary>
    void NotificarOperacao(string mensagem);
}

// ---------------------------------------------------------------- serviços de uso

public interface IGeracaoPecaService
{
    Task<Result<SubmissaoResponse>> SubmeterAsync(GerarPecaRequest request, CancellationToken cancellationToken = default);

    Task<Result<StatusPecaResponse>> ObterStatusAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PecaDetalheResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<StatusPecaResponse>> CancelarAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<SubmissaoResponse>> RegenerarAsync(Guid id, RegenerarPecaRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PecaResumoResponse>>> ListarAsync(int pagina, int tamanho, CancellationToken cancellationToken = default);

    /// <summary>Valida acesso e estado ANTES de abrir o stream (erro = HTTP 4xx); só vale enquanto em processamento.</summary>
    Task<Result<IAsyncEnumerable<GenerationEvent>>> AbrirStreamAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPecaMetricasService
{
    Task<Result<PecaMetricasResponse>> ObterAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- fila e GPU

public sealed record ReservedQueueItem(
    Guid Id, Guid TenantId, QueueItemKind Tipo, Guid ReferenciaId, int Tentativas, DateTimeOffset CriadoEm);

public interface IGeracaoPecaFila
{
    /// <summary>Adiciona o item ao contexto; quem chamou grava junto com o resto (atomicidade com a solicitação).</summary>
    void Enfileirar(Guid tenantId, QueueItemKind tipo, Guid referenciaId);

    /// <summary>
    /// Reserva até <paramref name="max"/> itens elegíveis (pendentes e disponíveis, ou com reserva
    /// vencida). A reserva é atômica por item (compare-and-set no banco): dois workers nunca levam o mesmo.
    /// </summary>
    Task<IReadOnlyList<ReservedQueueItem>> ReservarAsync(
        string worker, int max, TimeSpan reserva, CancellationToken cancellationToken = default);

    /// <summary>Reserva itens pendentes criados antes de <paramref name="criadosAntes"/> (para expirá-los sem usar a GPU).</summary>
    Task<IReadOnlyList<ReservedQueueItem>> ReservarExpiradosAsync(
        string worker, DateTimeOffset criadosAntes, int max, TimeSpan reserva, CancellationToken cancellationToken = default);

    /// <summary>Encerra o item (Concluido, Falhou ou Cancelado), liberando a reserva.</summary>
    Task FinalizarAsync(Guid itemId, QueueItemStatus status, string? erro, CancellationToken cancellationToken = default);

    Task CancelarPorReferenciaAsync(Guid referenciaId, CancellationToken cancellationToken = default);

    /// <summary>Devolve à fila, elegível só a partir de <paramref name="disponivelEm"/>.</summary>
    Task ReagendarAsync(
        Guid itemId, DateTimeOffset disponivelEm, string erro, bool consumirTentativa, CancellationToken cancellationToken = default);

    /// <summary>Itens que o worker pode pegar agora.</summary>
    Task<int> ContarDisponiveisAsync(CancellationToken cancellationToken = default);

    /// <summary>Pendentes (inclusive em backoff) + reservados: enquanto isto for &gt; 0 a GPU não pode ser parada.</summary>
    Task<int> ContarTrabalhoAbertoAsync(CancellationToken cancellationToken = default);

    /// <summary>Última vez que algo aconteceu na fila (criação, reserva, conclusão): base do idle timeout, durável entre reinícios.</summary>
    Task<DateTimeOffset?> UltimaAtividadeAsync(CancellationToken cancellationToken = default);
}

public enum WorkOutcomeKind
{
    Concluido,
    Cancelado,

    /// <summary>Falha transitória com tentativas sobrando: volta para a fila com backoff.</summary>
    Reagendar,

    /// <summary>Falha definitiva (ou tentativas esgotadas).</summary>
    Falhou,

    /// <summary>O processo foi desligado no meio: volta para a fila SEM consumir tentativa.</summary>
    Interrompido,
}

public sealed record WorkOutcome(WorkOutcomeKind Kind, string? Erro = null);

/// <summary>Quem sabe processar um tipo de item da fila (geração de peça, indexação de documento).</summary>
public interface IQueueWorkHandler
{
    QueueItemKind Tipo { get; }

    /// <summary>Chamado ao reservar: registra a tentativa e mostra "na fila"/"preparando IA" ao usuário.</summary>
    Task AoReservarAsync(Guid referenciaId, CancellationToken cancellationToken = default);

    Task AoPrepararGpuAsync(Guid referenciaId, CancellationToken cancellationToken = default);

    /// <summary>A GPU não subiu: devolve ao estado de espera, sem gastar tentativa.</summary>
    Task AoFaltarInfraAsync(Guid referenciaId, string motivo, CancellationToken cancellationToken = default);

    /// <param name="tentativa">Quantas vezes o item já foi reservado (esta inclusive): base do limite de tentativas.</param>
    Task<WorkOutcome> ProcessarAsync(Guid referenciaId, int tentativa, CancellationToken cancellationToken = default);

    /// <summary>Tempo de espera esgotado na fila: falha sem passar pela GPU.</summary>
    Task AoExpirarAsync(Guid referenciaId, CancellationToken cancellationToken = default);

    /// <summary>Tentativas esgotadas / erro definitivo na fila: registra a falha final do item.</summary>
    Task AoFalharDefinitivoAsync(Guid referenciaId, string erro, CancellationToken cancellationToken = default);
}

public sealed record VastInstanceInfo(
    string Id,
    string? ActualStatus,
    string? IntendedStatus,
    string? PublicIp,
    IReadOnlyDictionary<string, int> PublicPorts)
{
    public bool IsRunning => string.Equals(ActualStatus, "running", StringComparison.OrdinalIgnoreCase);

    public bool IsStopped =>
        string.Equals(ActualStatus, "exited", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ActualStatus, "stopped", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Controle da instância na Vast.ai. Idempotente: iniciar uma instância ativa ou parar uma parada é sucesso sem chamada.</summary>
public interface IVastAiService
{
    Task<Result<VastInstanceInfo>> ObterInstanciaAsync(CancellationToken cancellationToken = default);

    Task<Result> IniciarAsync(string motivo, CancellationToken cancellationToken = default);

    Task<Result> PararAsync(string motivo, CancellationToken cancellationToken = default);
}

public sealed record GpuSnapshot(string Estado, bool CircuitoAberto, DateTimeOffset? UltimoInicio);

/// <summary>Estado em memória da GPU: circuito de proteção e última subida. O que precisa sobreviver a reinício vem do banco.</summary>
public interface IGpuStateService
{
    bool CircuitoAberto(DateTimeOffset agora);

    void RegistrarFalha(string motivo, DateTimeOffset agora);

    void RegistrarSucesso();

    void RegistrarInicio(DateTimeOffset agora);

    /// <summary>Marca a GPU como em uso agora (ingestão do acervo usa sem passar pela fila): adia a parada por ociosidade.</summary>
    void RegistrarUso(DateTimeOffset agora);

    DateTimeOffset? UltimoInicio { get; }

    GpuSnapshot Snapshot(DateTimeOffset agora);
}

public interface IGpuOrchestrator
{
    /// <summary>
    /// Garante instância "running" e Ollama respondendo com os dois modelos. Inicia a instância se
    /// preciso (chamando <paramref name="aoIniciar"/> antes de esperar) e nunca a destrói.
    /// </summary>
    Task<Result> GarantirProntaAsync(
        string motivo, Func<CancellationToken, Task>? aoIniciar, CancellationToken cancellationToken = default);

    /// <summary>Para a instância se não há trabalho e a carência acabou. Devolve verdadeiro se parou.</summary>
    Task<bool> PararSeOciosaAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- acervo / ingestão

public interface IDocumentTextExtractor
{
    /// <summary>Extrai o texto por página (PDF) ou como um bloco só (DOCX/TXT, sem página). PDF escaneado sem texto é erro.</summary>
    Result<IReadOnlyList<ExtractedPage>> Extract(Stream content, string fileName);
}

public sealed record IndexDocumentRequest(
    Guid TenantId,
    Guid UsuarioId,
    Guid DocumentoOrigemId,
    string NomeArquivo,
    string TipoDocumento,
    string? Area,
    string? Tribunal,
    DateOnly? DataPublicacao,
    string? UrlOrigem,
    IReadOnlyList<string> Tags,
    string Visibilidade,
    IReadOnlyList<IndexPage> Paginas);

public sealed record IndexPage(int? Numero, string Texto);

public sealed record IndexDocumentResponse(Guid DocumentoOrigemId, int Trechos);

/// <summary>Núcleo da indexação: quebra em trechos, gera embeddings e grava no banco vetorial. Roda no worker, com a GPU ligada.</summary>
public interface IDocumentIndexer
{
    Task<Result<IndexDocumentResponse>> IndexAsync(IndexDocumentRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentIngestionService
{
    Task<Result<UploadDocumentoResponse>> EnviarAsync(
        UploadDocumentoRequest metadados, Stream conteudo, string nomeArquivo, long tamanhoBytes,
        CancellationToken cancellationToken = default);

    Task<Result<DocumentoResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DocumentoResponse>>> ListarAsync(int pagina, int tamanho, CancellationToken cancellationToken = default);

    Task<Result> RemoverAsync(Guid id, CancellationToken cancellationToken = default);
}
