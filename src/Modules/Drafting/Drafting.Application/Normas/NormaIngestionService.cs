using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Ingere UM Markdown: hash → (nada mudou? sai) → YAML + validação → chunks jurídicos → object storage →
/// PostgreSQL (versão, chunks) → embeddings BGE-M3 → Qdrant → só então marca como indexado e remove a
/// versão anterior. A ordem importa: a versão antiga só deixa de existir DEPOIS de a nova estar no
/// Qdrant, então a busca nunca fica sem a norma; e os ids dos pontos são determinísticos, então uma
/// execução interrompida pode ser repetida sem duplicar nada.
/// </summary>
public sealed class NormaIngestionService(
    INormaStore store,
    IObjectStorage storage,
    IEmbeddingService embeddings,
    INormaVectorStore vectors,
    NormaChunker chunker,
    IOptions<NormasIngestaoOptions> options,
    IGpuStateService gpu,
    IDateTimeProvider clock,
    ILogger<NormaIngestionService> logger) : INormaIngestionService
{
    public async Task<IngestaoArquivoResultado> IngerirAsync(
        RepoArquivo arquivo, string commit, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (arquivo.Bytes > settings.TamanhoMaximoArquivoMb * 1024L * 1024L)
        {
            return new IngestaoArquivoResultado(IngestaoResultado.Ignorado, "arquivo acima do tamanho máximo");
        }

        var bytes = await File.ReadAllBytesAsync(arquivo.CaminhoAbsoluto, cancellationToken);
        var hash = arquivo.Hash ?? Sha256(bytes);
        var existente = await store.ObterPorCaminhoAsync(arquivo.CaminhoRelativo, cancellationToken);

        if (existente is { Status: NormaStatus.Indexada } indexada && indexada.HashSha256 == hash)
        {
            return new IngestaoArquivoResultado(IngestaoResultado.SemMudanca);
        }

        var texto = new UTF8Encoding(false, false).GetString(bytes);
        var frontMatter = FrontMatterParser.Parse(texto);
        var resolucao = NormaMetadataResolver.Resolver(arquivo.CaminhoRelativo, frontMatter);
        if (!resolucao.Ok)
        {
            return new IngestaoArquivoResultado(IngestaoResultado.Ignorado, resolucao.Motivo);
        }

        var meta = resolucao.Metadata!;
        if (NormaFilePolicy.MotivoDeRecusa(meta, settings.Filtros) is { } recusa)
        {
            return new IngestaoArquivoResultado(IngestaoResultado.Ignorado, recusa);
        }

        var rascunhos = chunker.Dividir(meta, frontMatter.Corpo)
            .DistinctBy(chunk => chunk.Hash) // embedding de trecho duplicado só é pago uma vez
            .Select((chunk, indice) => chunk with { Ordem = indice })
            .ToList();
        if (rascunhos.Count == 0)
        {
            return new IngestaoArquivoResultado(IngestaoResultado.Ignorado, "sem trechos indexáveis");
        }

        var agora = clock.UtcNow;
        var chave = $"{settings.Storage.Prefixo}/markdown/{arquivo.CaminhoRelativo}";
        var mesmoConteudo = existente is not null && existente.HashSha256 == hash;
        var colecaoAnterior = existente?.Colecao;

        if (existente is not null && !mesmoConteudo)
        {
            // Preserva o texto da versão que vai ser substituída (auditoria e rollback).
            var chaveAntiga = $"{settings.Storage.Prefixo}/versoes/{existente.Id}/{existente.HashSha256}.md";
            await storage.CopyAsync(existente.ArquivoMarkdownPath, chaveAntiga, cancellationToken);
            var anterior = await store.ObterVersaoAsync(existente.Id, existente.VersaoAtual, cancellationToken);
            anterior?.Substituir(chaveAntiga, agora);
        }

        await storage.PutAsync(chave, bytes, "text/markdown; charset=utf-8", cancellationToken);

        NormaJuridica norma;
        if (existente is null)
        {
            norma = NormaJuridica.Criar(meta, arquivo.CaminhoRelativo, chave, hash, settings.Origem, agora);
            store.Adicionar(norma);
            store.Adicionar(new NormaVersao(norma.Id, norma.VersaoAtual, meta, hash, chave, commit, agora));
        }
        else if (!mesmoConteudo)
        {
            norma = existente;
            norma.Atualizar(meta, chave, hash, agora);
            store.Adicionar(new NormaVersao(norma.Id, norma.VersaoAtual, meta, hash, chave, commit, agora));
        }
        else
        {
            norma = existente;
            norma.ReabrirParaIndexacao(meta, chave, hash, agora);
            await store.RemoverChunksDaVersaoAsync(norma.Id, norma.VersaoAtual, cancellationToken);
        }

        var chunks = rascunhos
            .Select(r => new NormaChunk(
                norma.Id, norma.VersaoAtual, r.Ordem, r.Texto, r.Secao, r.Artigo, r.Paragrafo, r.Inciso, r.Tokens, r.Hash, agora))
            .ToList();
        store.Adicionar(chunks);
        await store.SalvarAsync(cancellationToken);

        try
        {
            await IndexarAsync(norma, meta, chave, chunks, settings, cancellationToken);
            if (colecaoAnterior is not null && !string.Equals(colecaoAnterior, meta.Colecao, StringComparison.Ordinal))
            {
                await vectors.DeleteAsync(colecaoAnterior, norma.Id, null, cancellationToken);
            }

            await store.ConcluirIndexacaoAsync(norma.Id, norma.VersaoAtual, cancellationToken);
            norma.MarcarIndexada(clock.UtcNow);
            await store.SalvarAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // O Markdown e os chunks já estão salvos: a próxima passada refaz só a indexação (mesmo hash).
            logger.LogWarning(ex, "Falha ao indexar {Caminho}; será retentado.", arquivo.CaminhoRelativo);
            norma.MarcarFalha(ex.Message, clock.UtcNow);
            await store.SalvarAsync(CancellationToken.None);

            return new IngestaoArquivoResultado(IngestaoResultado.Falhou, ex.GetType().Name);
        }

        return new IngestaoArquivoResultado(
            existente is null ? IngestaoResultado.Novo : IngestaoResultado.Atualizado, Chunks: chunks.Count);
    }

    private async Task IndexarAsync(
        NormaJuridica norma, NormaMetadata meta, string chave, List<NormaChunk> chunks,
        NormasIngestaoOptions settings, CancellationToken cancellationToken)
    {
        await vectors.EnsureCollectionsAsync(DocumentIndexer.EmbeddingDimensions, cancellationToken);

        var lote = Math.Max(1, settings.LoteEmbedding);
        for (var offset = 0; offset < chunks.Count; offset += lote)
        {
            var parte = chunks.Skip(offset).Take(lote).ToList();
            gpu.RegistrarUso(clock.UtcNow);

            var vetores = await embeddings.EmbedAsync(parte.Select(c => c.Texto).ToList(), cancellationToken);
            if (vetores.Count != parte.Count || vetores.Any(v => v.Length != DocumentIndexer.EmbeddingDimensions))
            {
                throw new InvalidOperationException("O serviço de embeddings devolveu vetores inesperados.");
            }

            var pontos = parte.Select((chunk, i) => new NormaVectorPoint(
                chunk.Id,
                vetores[i],
                new NormaPayload(
                    norma.Id, meta.Urn, meta.Titulo, meta.Tipo, meta.Numero, meta.Data, meta.Situacao, meta.Revogada,
                    meta.VersaoTexto, norma.VersaoAtual, meta.Esfera, meta.Uf, meta.Municipio, meta.Area, meta.Tribunal,
                    meta.Assuntos, meta.Colecao, chave, meta.FonteOficialUrl, null, chunk.Secao, chunk.Artigo,
                    chunk.Paragrafo, chunk.Inciso, chunk.HashSha256, chunk.Texto))).ToList();

            await vectors.UpsertAsync(meta.Colecao, pontos, cancellationToken);
        }

        // Só agora a versão anterior sai do índice: não existe instante sem a norma na busca.
        await vectors.DeleteAsync(meta.Colecao, norma.Id, norma.VersaoAtual, cancellationToken);
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
