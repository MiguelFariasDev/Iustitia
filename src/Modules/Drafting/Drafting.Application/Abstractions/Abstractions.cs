using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Abstractions;

/// <summary>Falha do serviço de IA (rede, HTTP, resposta inválida). A mensagem é técnica e NÃO vai ao usuário.</summary>
public sealed class LlmServiceException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record LlmMessage(string Role, string Content);

public sealed record LlmChatRequest(IReadOnlyList<LlmMessage> Messages);

/// <param name="Content">Texto novo deste pedaço (vazio no pedaço final).</param>
public sealed record LlmChunk(string Content, bool Done, int? PromptTokens, int? OutputTokens);

/// <summary>Geração de texto. Hoje o Ollama; a interface existe para trocar o modelo ou o provedor.</summary>
public interface ILlmService
{
    /// <summary>Nome do modelo configurado (gravado na solicitação).</summary>
    string ModelName { get; }

    IAsyncEnumerable<LlmChunk> ChatStreamAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Embeddings (BGE-M3, 1024 dimensões, via Ollama).</summary>
public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}

/// <summary>Banco vetorial (Qdrant hoje). Toda busca e toda exclusão são escopadas por tenant.</summary>
public interface IVectorStore
{
    Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchQuery query, CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);

    Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default);
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

public interface ILegalDocumentRepository : IRepository<LegalDocumentRequest, LegalDocumentRequestId>
{
    /// <summary>
    /// Marca um filho NOVO como Added de forma explícita. Sem isto, o EF trata um filho com chave já
    /// preenchida, descoberto na coleção de um pai que já existe, como "existente" e gera UPDATE
    /// (DbUpdateConcurrencyException) em vez de INSERT.
    /// </summary>
    void AddChild(RetrievedSource source);

    void AddChild(LegalDocumentEvent evento);
}

public interface IDraftingUnitOfWork : IUnitOfWork
{
}

public interface ILegalDocumentService
{
    Task<Result<LegalDocumentRequest>> CreateAsync(PecaInput input, CancellationToken cancellationToken = default);

    Task StartAsync(LegalDocumentRequest request, string promptVersao, string modelo, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        LegalDocumentRequest request, string texto, long tempoMs, IReadOnlyList<(RetrievedChunk Chunk, bool Citada)> fontes,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        LegalDocumentRequest request, string erro, string? textoParcial, long tempoMs,
        IReadOnlyList<(RetrievedChunk Chunk, bool Citada)> fontes, CancellationToken cancellationToken = default);
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

public sealed record PreparedGeneration(LegalDocumentRequest Request, PecaInput Input);

public interface IPecaGenerationService
{
    /// <summary>Valida, sanitiza e persiste a solicitação. Falhas aqui viram HTTP 4xx ANTES de o stream abrir.</summary>
    Task<Result<PreparedGeneration>> PrepareAsync(GerarPecaRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<GenerationEvent> StreamAsync(PreparedGeneration prepared, CancellationToken cancellationToken = default);

    Task<Result<GerarPecaResponse>> GenerateAsync(PreparedGeneration prepared, CancellationToken cancellationToken = default);
}

public sealed record IndexDocumentRequest(
    Guid DocumentoOrigemId,
    string NomeArquivo,
    string TipoDocumento,
    string? Area,
    string? Tribunal,
    DateOnly? DataPublicacao,
    string? UrlOrigem,
    IReadOnlyList<IndexPage> Paginas);

public sealed record IndexPage(int Numero, string Texto);

public sealed record IndexDocumentResponse(Guid DocumentoOrigemId, int Trechos);

public interface IDocumentIndexer
{
    Task<Result<IndexDocumentResponse>> IndexAsync(IndexDocumentRequest request, CancellationToken cancellationToken = default);
}
