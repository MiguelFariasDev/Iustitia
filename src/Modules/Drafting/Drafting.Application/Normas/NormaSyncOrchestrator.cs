using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Um ciclo de sincronização: atualiza o clone parcial → classifica cada Markdown pelo SHA-256 (novo,
/// alterado, sem mudança, removido) → liga a GPU SÓ se houver o que embutir → processa em ordem de
/// prioridade, em paralelismo limitado, em fatias (<c>LimiteArquivosPorCiclo</c>) → marca removidos.
/// Nunca reprocessa o que não mudou e nunca reindexa o acervo inteiro.
/// </summary>
public sealed class NormaSyncOrchestrator(
    IRepositorySync repositorySync,
    IServiceScopeFactory scopeFactory,
    IOptions<NormasIngestaoOptions> options,
    IDateTimeProvider clock,
    ILogger<NormaSyncOrchestrator> logger) : INormaSyncOrchestrator
{
    /// <summary>Falhas de infraestrutura seguidas que abortam o ciclo (GPU/Qdrant fora do ar não se resolve arquivo a arquivo).</summary>
    private const int FalhasSeguidasParaAbortar = 5;

    private const int SalvarProgressoACada = 50;

    public async Task<SincronizacaoResumo> SincronizarAsync(
        IngestaoJobTipo tipo, Guid? jobId = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        await using var jobScope = scopeFactory.CreateAsyncScope();
        var jobStore = jobScope.ServiceProvider.GetRequiredService<INormaStore>();

        var job = jobId is { } id ? await jobStore.ObterJobAsync(id, cancellationToken) : null;
        var escopo = string.Join(", ", settings.PastasIncluidas);
        if (job is null)
        {
            job = new IngestaoJob(tipo, escopo, clock.UtcNow);
            jobStore.Adicionar(job);
        }
        else
        {
            job.Iniciar(escopo, clock.UtcNow);
        }

        await jobStore.SalvarAsync(cancellationToken);

        try
        {
            return await ExecutarAsync(job, jobStore, tipo, settings, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            job.Concluir(clock.UtcNow, interrompido: true);
            await jobStore.SalvarAsync(CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sincronização do acervo falhou.");
            job.Falhar(ex.Message, clock.UtcNow);
            await jobStore.SalvarAsync(CancellationToken.None);

            return new SincronizacaoResumo(job.Id, job.TotalArquivos, 0, 0, 0, job.ArquivosIgnorados, 0, job.ArquivosComErro + 1, true);
        }
    }

    private async Task<SincronizacaoResumo> ExecutarAsync(
        IngestaoJob job, INormaStore jobStore, IngestaoJobTipo tipo, NormasIngestaoOptions settings, CancellationToken ct)
    {
        var snapshot = await repositorySync.SincronizarAsync(ct);
        var elegiveis = snapshot.Arquivos
            .Where(a => NormaFilePolicy.Elegivel(a.CaminhoRelativo, settings))
            .OrderBy(a => NormaFilePolicy.Prioridade(a.CaminhoRelativo))
            .ThenBy(a => a.CaminhoRelativo, StringComparer.Ordinal)
            .ToList();
        job.DefinirTotal(elegiveis.Count, snapshot.Commit);

        var estado = (await jobStore.ListarEstadoAsync(ct)).ToDictionary(e => e.RepoPath, StringComparer.Ordinal);

        // Classificação por hash: lê cada arquivo uma vez, sem GPU e sem tocar no banco além do mapa em memória.
        // Arquivo que NÃO serve (sem conteúdo, fora do recorte, grande demais) é descartado aqui, para não
        // ligar a GPU por causa dele a cada ciclo.
        var pendentes = new List<RepoArquivo>();
        var semMudanca = 0;
        var ignorados = 0;
        var limiteBytes = settings.TamanhoMaximoArquivoMb * 1024L * 1024L;
        foreach (var arquivo in elegiveis)
        {
            ct.ThrowIfCancellationRequested();
            if (arquivo.Bytes > limiteBytes)
            {
                ignorados++;
                job.Ignorado();
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(arquivo.CaminhoAbsoluto, ct);
            var hash = NormaIngestionService.Sha256(bytes);
            if (tipo != IngestaoJobTipo.Reprocessamento
                && estado.TryGetValue(arquivo.CaminhoRelativo, out var atual)
                && atual.Status == NormaStatus.Indexada && atual.Hash == hash)
            {
                semMudanca++;
                continue;
            }

            if (MotivoDeDescarte(arquivo.CaminhoRelativo, bytes, settings) is not null)
            {
                ignorados++;
                job.Ignorado();
                continue;
            }

            pendentes.Add(arquivo with { Hash = hash });
        }

        var presentes = elegiveis.Select(a => a.CaminhoRelativo).ToHashSet(StringComparer.Ordinal);
        var removidos = estado.Values
            .Where(e => e.Status != NormaStatus.Removida && NormaFilePolicy.Elegivel(e.RepoPath, settings) && !presentes.Contains(e.RepoPath))
            .ToList();

        var parcial = pendentes.Count > settings.LimiteArquivosPorCiclo;
        var lote = pendentes.Take(settings.LimiteArquivosPorCiclo).ToList();
        logger.LogInformation(
            "Acervo: {Total} Markdowns, {SemMudanca} sem mudança, {Pendentes} a processar ({Lote} neste ciclo), {Removidos} removidos.",
            elegiveis.Count, semMudanca, pendentes.Count, lote.Count, removidos.Count);

        var novos = 0;
        var atualizados = 0;
        var erros = 0;
        var abortado = false;

        if (lote.Count > 0)
        {
            await using var gpuScope = scopeFactory.CreateAsyncScope();
            var gpuReady = await gpuScope.ServiceProvider.GetRequiredService<IGpuOrchestrator>()
                .GarantirProntaAsync("Ingestão do acervo normativo", null, ct);
            if (gpuReady.IsFailure)
            {
                job.Falhar($"GPU indisponível: {gpuReady.Error.Message}", clock.UtcNow);
                await jobStore.SalvarAsync(ct);

                return new SincronizacaoResumo(job.Id, elegiveis.Count, 0, 0, semMudanca, 0, 0, 0, true);
            }

            using var gate = new SemaphoreSlim(1, 1);
            var processados = 0;
            var falhasSeguidas = 0;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await Parallel.ForEachAsync(
                    lote,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, settings.ParalelismoArquivos), CancellationToken = cts.Token },
                    async (arquivo, token) =>
                    {
                        IngestaoArquivoResultado resultado;
                        try
                        {
                            // Escopo próprio por arquivo: DbContext não é thread-safe e o tenant ambiente é por fluxo.
                            await using var scope = scopeFactory.CreateAsyncScope();
                            resultado = await scope.ServiceProvider.GetRequiredService<INormaIngestionService>()
                                .IngerirAsync(arquivo, snapshot.Commit, token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Erro inesperado ao ingerir {Caminho}.", arquivo.CaminhoRelativo);
                            resultado = new IngestaoArquivoResultado(IngestaoResultado.Falhou, ex.GetType().Name);
                        }

                        await gate.WaitAsync(token);
                        try
                        {
                            switch (resultado.Resultado)
                            {
                                case IngestaoResultado.Novo:
                                    novos++;
                                    job.Processado();
                                    falhasSeguidas = 0;
                                    break;
                                case IngestaoResultado.Atualizado:
                                    atualizados++;
                                    job.Processado();
                                    falhasSeguidas = 0;
                                    break;
                                case IngestaoResultado.SemMudanca:
                                    semMudanca++;
                                    break;
                                case IngestaoResultado.Ignorado:
                                    ignorados++;
                                    job.Ignorado();
                                    logger.LogDebug("Ignorado {Caminho}: {Motivo}", arquivo.CaminhoRelativo, resultado.Motivo);
                                    break;
                                default:
                                    erros++;
                                    job.ComErro();
                                    if (++falhasSeguidas >= FalhasSeguidasParaAbortar)
                                    {
                                        abortado = true;
                                        await cts.CancelAsync();
                                    }

                                    break;
                            }

                            if (++processados % SalvarProgressoACada == 0)
                            {
                                await jobStore.SalvarAsync(CancellationToken.None);
                            }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    });
            }
            catch (OperationCanceledException) when (abortado && !ct.IsCancellationRequested)
            {
                logger.LogError("Ciclo abortado após {N} falhas seguidas (GPU ou Qdrant fora do ar?).", FalhasSeguidasParaAbortar);
            }
        }

        var removidosCount = 0;
        foreach (var removido in abortado ? [] : removidos)
        {
            ct.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            if (await RemoverAsync(scope.ServiceProvider, removido, ct))
            {
                removidosCount++;
                job.Removido();
            }
        }

        job.Concluir(clock.UtcNow);
        if (abortado)
        {
            job.Falhar("Abortado por falhas seguidas de infraestrutura (veja os logs).", clock.UtcNow);
        }

        await jobStore.SalvarAsync(CancellationToken.None);

        return new SincronizacaoResumo(
            job.Id, elegiveis.Count, novos, atualizados, semMudanca, ignorados, removidosCount, erros, parcial || abortado);
    }

    /// <summary>Arquivo sumiu do repositório: sai da busca (Qdrant e chunks), mas norma, versões e Markdown ficam para auditoria.</summary>
    private async Task<bool> RemoverAsync(IServiceProvider services, NormaEstado removido, CancellationToken ct)
    {
        var store = services.GetRequiredService<INormaStore>();
        var vectors = services.GetRequiredService<INormaVectorStore>();
        var norma = await store.ObterPorCaminhoAsync(removido.RepoPath, ct);
        if (norma is null)
        {
            return false;
        }

        try
        {
            await vectors.DeleteAsync(norma.Colecao, norma.Id, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível remover {Caminho} do Qdrant; será retentado.", removido.RepoPath);

            return false;
        }

        await store.RemoverChunksDaVersaoAsync(norma.Id, norma.VersaoAtual, ct);
        norma.MarcarRemovida(clock.UtcNow);
        await store.SalvarAsync(ct);

        return true;
    }

    private static string? MotivoDeDescarte(string caminho, byte[] bytes, NormasIngestaoOptions settings)
    {
        var frontMatter = FrontMatterParser.Parse(new System.Text.UTF8Encoding(false, false).GetString(bytes));
        var resolucao = NormaMetadataResolver.Resolver(caminho, frontMatter);

        return resolucao.Ok ? NormaFilePolicy.MotivoDeRecusa(resolucao.Metadata!, settings.Filtros) : resolucao.Motivo;
    }
}
