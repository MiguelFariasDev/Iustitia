using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Normas;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Object storage barato (Cloudflare R2, Backblaze B2 ou disco em desenvolvimento) para os Markdowns
/// e, só sob demanda, os originais. Chaves são relativas ao prefixo configurado.
/// </summary>
public interface IObjectStorage
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default);
}

public sealed record RepoArquivo(string CaminhoRelativo, string CaminhoAbsoluto, long Bytes, string? Hash = null);

public sealed record RepoSnapshot(string Commit, IReadOnlyList<RepoArquivo> Arquivos);

/// <summary>Clone parcial (sparse, shallow) e atualização do repositório de origem.</summary>
public interface IRepositorySync
{
    /// <summary>Clona na primeira vez, atualiza nas seguintes, e lista os Markdowns elegíveis do disco.</summary>
    Task<RepoSnapshot> SincronizarAsync(CancellationToken cancellationToken = default);
}

public sealed record NormaEstado(Guid Id, string RepoPath, string Hash, NormaStatus Status, string Colecao);

/// <summary>Persistência da ingestão. Escopo por arquivo: uma instância por unidade de trabalho.</summary>
public interface INormaStore
{
    Task<IReadOnlyList<NormaEstado>> ListarEstadoAsync(CancellationToken cancellationToken = default);

    Task<NormaJuridica?> ObterPorCaminhoAsync(string repoPath, CancellationToken cancellationToken = default);

    Task<NormaVersao?> ObterVersaoAsync(Guid normaId, int numero, CancellationToken cancellationToken = default);

    void Adicionar(NormaJuridica norma);

    void Adicionar(NormaVersao versao);

    void Adicionar(IngestaoJob job);

    void Adicionar(IEnumerable<NormaChunk> chunks);

    /// <summary>Apaga os chunks da norma na versão dada (retentativa idempotente).</summary>
    Task RemoverChunksDaVersaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default);

    /// <summary>Marca os chunks da versão como indexados e apaga os das versões anteriores.</summary>
    Task ConcluirIndexacaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default);

    Task<IngestaoJob?> ObterJobAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Próximo pedido manual de sincronização ainda não iniciado.</summary>
    Task<IngestaoJob?> ObterJobPendenteAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngestaoJob>> ListarJobsAsync(int limite, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> UltimaSincronizacaoAsync(CancellationToken cancellationToken = default);

    Task<NormaJuridica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> SalvarAsync(CancellationToken cancellationToken = default);
}

/// <summary>Filtros estruturados da busca (valem para o Qdrant e para o PostgreSQL).</summary>
public sealed record NormaFiltro(
    IReadOnlyList<string>? Colecoes = null,
    IReadOnlyList<string>? Tipos = null,
    IReadOnlyList<string>? Tribunais = null,
    IReadOnlyList<string>? Esferas = null,
    IReadOnlyList<string>? Ufs = null,
    IReadOnlyList<string>? Municipios = null,
    string? Area = null,
    bool AreaEstrita = false,
    DateOnly? DataDe = null,
    DateOnly? DataAte = null,
    bool IncluirRevogadas = true,
    IReadOnlyList<string>? Assuntos = null,
    IReadOnlyList<Guid>? NormaIds = null)
{
    public IReadOnlyList<string> ColecoesEfetivas => Colecoes is { Count: > 0 } ? Colecoes : global::Advocacia.Modules.Drafting.Application.Normas.Colecoes.Publicas;
}

/// <summary>Tudo que vai no payload do Qdrant e volta nos hits. Fonte única do mapeamento (Qdrant e SQL).</summary>
public sealed record NormaPayload(
    Guid NormaJuridicaId,
    string? Urn,
    string Titulo,
    string Tipo,
    string? Numero,
    DateOnly? Data,
    string Situacao,
    bool Revogada,
    string VersaoTexto,
    int VersaoNumero,
    string Esfera,
    string? Uf,
    string? Municipio,
    string? Area,
    string? Tribunal,
    IReadOnlyList<string> Assuntos,
    string Colecao,
    string ArquivoMarkdownPath,
    string FonteOficialUrl,
    int? Pagina,
    string? Secao,
    string? Artigo,
    string? Paragrafo,
    string? Inciso,
    string HashConteudo,
    string Texto)
{
    public VectorHit ToHit(string pointId, double score) =>
        new(pointId, Texto, score, new ChunkMetadata(
            DocumentoOrigemId: null,
            NomeArquivo: Titulo,
            Pagina: Pagina,
            TipoDocumento: Tipo,
            Area: Area,
            Tribunal: Tribunal,
            DataPublicacao: Data,
            UrlOrigem: string.IsNullOrWhiteSpace(FonteOficialUrl) ? null : FonteOficialUrl,
            HashConteudo: HashConteudo,
            Tags: Assuntos,
            Norma: new NormaRef(
                NormaJuridicaId, Titulo, Tipo, Numero, Urn, Situacao, Revogada, VersaoTexto, Esfera, Uf, Municipio,
                Colecao, ArquivoMarkdownPath, Artigo, Paragrafo, Inciso, Secao, VersaoNumero)));
}

public sealed record NormaVectorPoint(Guid Id, float[] Vector, NormaPayload Payload);

/// <summary>Banco vetorial do acervo PÚBLICO (sem tenant). Documentos de cliente usam <c>IVectorStore</c>.</summary>
public interface INormaVectorStore
{
    /// <summary>Cria as coleções que faltam (int8, vetores em disco, índices de payload). Idempotente.</summary>
    Task EnsureCollectionsAsync(int dimensions, CancellationToken cancellationToken = default);

    Task UpsertAsync(string colecao, IReadOnlyList<NormaVectorPoint> points, CancellationToken cancellationToken = default);

    /// <summary>Apaga os pontos da norma; com <paramref name="manterVersao"/>, preserva essa versão.</summary>
    Task DeleteAsync(string colecao, Guid normaId, int? manterVersao, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> SearchAsync(
        NormaFiltro filtro, float[] vetor, int limit, CancellationToken cancellationToken = default);

    Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Busca por palavra-chave (full-text do PostgreSQL) e conferências de existência.</summary>
public interface INormaSearchIndex
{
    Task<IReadOnlyList<VectorHit>> BuscarPalavrasChaveAsync(
        NormaFiltro filtro, string consulta, int limit, CancellationToken cancellationToken = default);

    Task<bool> UrnExisteAsync(string urn, CancellationToken cancellationToken = default);
}

public sealed record NormaBusca(string TextoConsulta, NormaFiltro Filtro);

public interface IHybridSearchService
{
    /// <summary>Candidatos fundidos, deduplicados e reordenados — já no tamanho final (5–10).</summary>
    Task<IReadOnlyList<VectorHit>> BuscarAsync(NormaBusca busca, float[] vetor, CancellationToken cancellationToken = default);
}

public enum IngestaoResultado
{
    Novo,
    Atualizado,
    SemMudanca,
    Ignorado,
    Falhou,
}

public sealed record IngestaoArquivoResultado(IngestaoResultado Resultado, string? Motivo = null, int Chunks = 0);

public interface INormaIngestionService
{
    Task<IngestaoArquivoResultado> IngerirAsync(RepoArquivo arquivo, string commit, CancellationToken cancellationToken = default);
}

public sealed record SincronizacaoResumo(
    Guid JobId, int Total, int Novos, int Atualizados, int SemMudanca, int Ignorados, int Removidos, int Erros, bool Parcial);

public interface INormaSyncOrchestrator
{
    Task<SincronizacaoResumo> SincronizarAsync(IngestaoJobTipo tipo, Guid? jobId = null, CancellationToken cancellationToken = default);
}
