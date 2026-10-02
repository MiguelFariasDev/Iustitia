using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Cliente da API Pública do DataJud (CNJ). USO RESTRITO A PORTFÓLIO — ver ADR-064 e
/// docs/compliance/datajud-termo.md.
///
/// O HttpClient já chega configurado (BaseAddress, header Authorization, resiliência Polly)
/// pelo registro em DependencyInjection.
///
/// NUNCA loga o corpo da resposta: os metadados incluem partes, assuntos e movimentos de um
/// processo real, e um deles pode estar em segredo de justiça. O log carrega tribunal,
/// desfecho e latência — o suficiente para diagnosticar sem expor o caso.
/// </summary>
public sealed class DataJudClient(
    HttpClient httpClient,
    ICacheService cacheService,
    DataJudTelemetry telemetry,
    IOptions<DataJudOptions> options,
    ILogger<DataJudClient> logger) : IDataJudClient
{
    public const string HttpClientName = "datajud";

    private readonly DataJudOptions _options = options.Value;

    public async Task<Result<DataJudProcesso?>> ConsultarProcessoAsync(
        string tribunal, string numeroProcesso, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroProcesso) || numeroProcesso.Length != 20 || !numeroProcesso.All(char.IsAsciiDigit))
        {
            return Result.Failure<DataJudProcesso?>(ErrorFactory.From(ErrorCode.CNJ_INVALID_REQUEST));
        }

        var searchPath = DataJudEndpoints.GetSearchPath(tribunal);
        if (searchPath is null)
        {
            return Result.Failure<DataJudProcesso?>(ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, tribunal));
        }

        var cacheKey = $"datajud:{tribunal.ToLowerInvariant()}:{numeroProcesso}";

        // O cache guarda o ENVELOPE, não o processo, para poder representar também o
        // "não encontrado" — que é a resposta mais cara de repetir, já que o usuário tende a
        // reconsultar o mesmo número achando que errou.
        var emCache = await cacheService.GetAsync<CachedResult>(cacheKey, cancellationToken);
        if (emCache is not null)
        {
            telemetry.RecordCacheHit(tribunal);
            return Result.Success(emCache.Processo);
        }

        using var activity = DataJudTelemetry.StartConsulta(tribunal);
        var cronometro = Stopwatch.StartNew();
        telemetry.RecordRequest(tribunal);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(
                searchPath, DataJudSearchRequest.ByNumeroProcesso(numeroProcesso), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            var codigo = exception is TaskCanceledException ? ErrorCode.CNJ_TIMEOUT : ErrorCode.CNJ_UNAVAILABLE;
            telemetry.RecordFailure(tribunal, codigo.ToString());
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogWarning(exception, "DataJud: consulta ao {Tribunal} falhou após {Elapsed}ms", tribunal, cronometro.ElapsedMilliseconds);
            return Result.Failure<DataJudProcesso?>(ErrorFactory.From(codigo));
        }

        using (response)
        {
            cronometro.Stop();
            telemetry.RecordDuration(tribunal, cronometro.Elapsed.TotalMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var codigo = MapStatusCode(response.StatusCode);
                if (codigo == ErrorCode.CNJ_RATE_LIMITED)
                {
                    telemetry.RecordRateLimited(tribunal);
                }

                telemetry.RecordFailure(tribunal, codigo.ToString());
                activity?.SetStatus(ActivityStatusCode.Error);
                logger.LogWarning(
                    "DataJud: consulta ao {Tribunal} respondeu {StatusCode} em {Elapsed}ms",
                    tribunal, (int)response.StatusCode, cronometro.ElapsedMilliseconds);
                return Result.Failure<DataJudProcesso?>(ErrorFactory.From(codigo));
            }

            DataJudSearchResponse? payload;
            using (DataJudTelemetry.StartParse())
            {
                try
                {
                    payload = await response.Content.ReadFromJsonAsync<DataJudSearchResponse>(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    telemetry.RecordFailure(tribunal, nameof(ErrorCode.CNJ_INVALID_RESPONSE));
                    logger.LogWarning(exception, "DataJud: resposta do {Tribunal} não pôde ser interpretada", tribunal);
                    return Result.Failure<DataJudProcesso?>(ErrorFactory.From(ErrorCode.CNJ_INVALID_RESPONSE));
                }
            }

            var processo = Mapear(payload, tribunal);
            if (processo is null)
            {
                telemetry.RecordNotFound(tribunal);
                logger.LogInformation("DataJud: processo não encontrado no {Tribunal} ({Elapsed}ms)", tribunal, cronometro.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "DataJud: processo encontrado no {Tribunal} com {Movimentos} movimentos ({Elapsed}ms)",
                    tribunal, processo.Movimentos.Count, cronometro.ElapsedMilliseconds);
            }

            await cacheService.SetAsync(
                cacheKey, new CachedResult(processo), TimeSpan.FromMinutes(_options.CacheTtlMinutes), cancellationToken);

            return Result.Success(processo);
        }
    }

    private static DataJudProcesso? Mapear(DataJudSearchResponse? payload, string tribunal)
    {
        var source = payload?.Hits?.Items?.FirstOrDefault()?.Source;
        if (source?.NumeroProcesso is null)
        {
            return null;
        }

        return new DataJudProcesso(
            source.NumeroProcesso,
            // O tribunal do payload é o que o próprio tribunal declarou; o nosso veio do
            // número CNJ. Preferimos o do payload por ser a fonte, com o nosso como reserva.
            string.IsNullOrWhiteSpace(source.Tribunal) ? tribunal : source.Tribunal,
            source.Grau,
            source.DataAjuizamento,
            source.NivelSigilo,
            Tabelado(source.Classe),
            Tabelado(source.Sistema),
            Tabelado(source.Formato),
            source.OrgaoJulgador is { Nome: not null } orgao
                ? new DataJudOrgaoJulgador(orgao.Codigo, orgao.Nome, orgao.CodigoMunicipioIBGE)
                : null,
            source.Assuntos?.Select(Tabelado).OfType<DataJudItemTabelado>().ToArray() ?? [],
            // Do mais recente para o mais antigo: é a ordem em que a timeline é lida.
            source.Movimentos?
                .Select(movimento => new DataJudMovimento(movimento.Codigo, movimento.Nome ?? string.Empty, movimento.DataHora))
                .OrderByDescending(movimento => movimento.DataHora ?? DateTimeOffset.MinValue)
                .ToArray() ?? []);
    }

    private static DataJudItemTabelado? Tabelado(DataJudTabelado? item) =>
        item?.Nome is { } nome ? new DataJudItemTabelado(item.Codigo, nome) : null;

    private static ErrorCode MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorCode.CNJ_AUTH_FAILED,
        HttpStatusCode.TooManyRequests => ErrorCode.CNJ_RATE_LIMITED,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ErrorCode.CNJ_TIMEOUT,
        HttpStatusCode.BadRequest or HttpStatusCode.NotFound => ErrorCode.CNJ_INVALID_REQUEST,
        _ => ErrorCode.CNJ_UNAVAILABLE,
    };

    /// <summary>
    /// Envelope do cache. Existe para distinguir "nunca consultei" (ausente no cache) de
    /// "consultei e não existe" (presente, com Processo nulo) — sem ele, todo processo
    /// inexistente seria reconsultado a cada tentativa do usuário.
    /// </summary>
    private sealed record CachedResult(DataJudProcesso? Processo);
}
