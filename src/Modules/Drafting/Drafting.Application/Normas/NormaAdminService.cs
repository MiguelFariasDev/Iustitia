using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Domain.Normas;

namespace Advocacia.Modules.Drafting.Application.Normas;

public sealed record NormaResponse(
    Guid Id,
    string Titulo,
    string Tipo,
    string? Numero,
    string? Urn,
    string Situacao,
    bool Revogada,
    string VersaoTexto,
    int Versao,
    string FonteOficialUrl,
    string Arquivo,
    string Esfera,
    string? Uf,
    string? Municipio,
    string? Tribunal,
    DateTimeOffset DataIngestao,
    DateTimeOffset DataUltimaAtualizacao,
    string Aviso);

public sealed record IngestaoJobResponse(
    Guid Id, string Tipo, string Status, int Total, int Processados, int ComErro, int Ignorados, int Removidos,
    string? Commit, DateTimeOffset IniciadoEm, DateTimeOffset? ConcluidoEm, string? Erro);

public sealed record SincronizacaoSolicitada(Guid Id, string Status);

public interface INormaAdminService
{
    Task<Result<SincronizacaoSolicitada>> SolicitarSincronizacaoAsync(bool reprocessar, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<IngestaoJobResponse>>> ListarJobsAsync(int limite, CancellationToken cancellationToken = default);

    Task<Result<NormaResponse>> ObterNormaAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Operação do acervo: pedir sincronização (o worker executa), ver o histórico e consultar a norma por trás de
/// uma citação. Não expõe a chave do object storage nem endereços internos — só o caminho no repositório de origem.
/// </summary>
public sealed class NormaAdminService(INormaStore store, IDateTimeProvider clock) : INormaAdminService
{
    public const string AvisoFonte =
        "Texto informativo, obtido por conversão automática (e, em alguns casos, OCR) do repositório ctresb/leis. " +
        "Não substitui a publicação oficial: confira sempre na fonte oficial indicada.";

    public async Task<Result<SincronizacaoSolicitada>> SolicitarSincronizacaoAsync(bool reprocessar, CancellationToken cancellationToken = default)
    {
        // Um pedido pendente basta: vários cliques não enfileiram vários ciclos.
        var existente = await store.ObterJobPendenteAsync(cancellationToken);
        if (existente is not null)
        {
            return Result.Success(new SincronizacaoSolicitada(existente.Id, existente.Status.ToString()));
        }

        var job = IngestaoJob.Solicitar(reprocessar ? IngestaoJobTipo.Reprocessamento : IngestaoJobTipo.Incremental, clock.UtcNow);
        store.Adicionar(job);
        await store.SalvarAsync(cancellationToken);

        return Result.Success(new SincronizacaoSolicitada(job.Id, job.Status.ToString()));
    }

    public async Task<Result<IReadOnlyList<IngestaoJobResponse>>> ListarJobsAsync(int limite, CancellationToken cancellationToken = default)
    {
        var jobs = await store.ListarJobsAsync(limite, cancellationToken);

        return Result.Success<IReadOnlyList<IngestaoJobResponse>>(jobs.Select(j => new IngestaoJobResponse(
            j.Id, j.Tipo.ToString(), j.Status.ToString(), j.TotalArquivos, j.ArquivosProcessados, j.ArquivosComErro,
            j.ArquivosIgnorados, j.ArquivosRemovidos, j.CommitOrigem, j.IniciadoEm, j.ConcluidoEm, j.Erro)).ToList());
    }

    public async Task<Result<NormaResponse>> ObterNormaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var n = await store.ObterPorIdAsync(id, cancellationToken);
        if (n is null || n.Status == NormaStatus.Removida)
        {
            return Result.Failure<NormaResponse>(ErrorFactory.From(ErrorCode.DRAFTING_NORMA_NOT_FOUND));
        }

        return Result.Success(new NormaResponse(
            n.Id, n.Titulo, n.Tipo, n.Numero, n.Urn, n.Situacao, n.Revogada, n.VersaoTexto, n.VersaoAtual, n.FonteOficialUrl,
            n.RepoPath, n.Esfera, n.Uf, n.Municipio, n.Tribunal, n.DataIngestao, n.DataUltimaAtualizacao, AvisoFonte));
    }
}
