using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;

/// <summary>
/// Cliente HTTP da API pública de comunicações do DJEN/CNJ. A resiliência (retry, circuit
/// breaker, timeout) é aplicada pelo pipeline registrado no HttpClient nomeado — ver
/// <see cref="DependencyInjection.AddCnjIntegration"/> —, não aqui dentro.
///
/// NUNCA loga o campo <c>texto</c> da publicação: é o inteiro teor de uma comunicação
/// processual e pode conter dados pessoais e sigilosos (ver claude.md, seção 14). Os logs
/// deste cliente carregam apenas contadores, datas, tribunal e o ExternalId.
/// </summary>
public sealed partial class DjenClient(
    HttpClient httpClient,
    IOptions<CnjOptions> options,
    ILogger<DjenClient> logger) : ICnjClient
{
    public const string HttpClientName = "Cnj.Djen";

    /// <summary>
    /// UnsafeRelaxedJsonEscaping para NÃO escapar acentuação como \uXXXX: o metadata vai
    /// para uma coluna jsonb e para logs de diagnóstico, nunca para dentro de HTML, então a
    /// preocupação que o encoder padrão endereça (XSS ao interpolar JSON em uma página) não
    /// se aplica — e "1ª Vara Cível" é bem mais consultável que "1\u00AA Vara C\u00EDvel".
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Opções de LEITURA do payload do DJEN — sem o encoder relaxado, que só afeta escrita.</summary>
    internal static readonly JsonSerializerOptions DeserializerOptions = new(JsonSerializerDefaults.Web);

    private readonly CnjOptions _options = options.Value;

    public async Task<Result<CnjPublicationPage>> GetPublicationsAsync(
        CnjPublicationQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.HasCriteria)
        {
            // Sem período e sem processo, a consulta puxaria o diário nacional inteiro.
            // Falhar aqui é deliberado: é erro de programação, mas devolvido como Result
            // para o job contabilizar e seguir, em vez de derrubar a execução inteira.
            logger.LogError("DJEN: consulta sem nenhum critério de recorte (nem período, nem processo) — recusada.");
            return Result.Failure<CnjPublicationPage>(ErrorFactory.From(ErrorCode.CNJ_INVALID_REQUEST));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, _options.MaxPageSize);

        var requestUri = BuildRequestUri(query, page, pageSize);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // TaskCanceledException sem cancelamento do chamador = timeout do HttpClient/Polly.
            logger.LogWarning("DJEN: timeout ao consultar comunicações ({Criterio}, página {Page}).", query.Describe(), page);
            return Result.Failure<CnjPublicationPage>(ErrorFactory.From(ErrorCode.CNJ_TIMEOUT));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "DJEN: falha de rede ao consultar comunicações ({Criterio}, página {Page}).", query.Describe(), page);
            return Result.Failure<CnjPublicationPage>(ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<CnjPublicationPage>(MapStatusCode(response.StatusCode, query, page));
            }

            DjenResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonSafeAsync(cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "DJEN: resposta em formato inesperado ({Criterio}, página {Page}).", query.Describe(), page);
                return Result.Failure<CnjPublicationPage>(ErrorFactory.From(ErrorCode.CNJ_INVALID_RESPONSE));
            }

            if (payload is null)
            {
                return Result.Failure<CnjPublicationPage>(ErrorFactory.From(ErrorCode.CNJ_INVALID_RESPONSE));
            }

            var items = (payload.Items ?? [])
                .Select(MapItem)
                .OfType<CnjPublicationDto>()
                .ToList();

            var discarded = (payload.Items?.Count ?? 0) - items.Count;
            if (discarded > 0)
            {
                logger.LogWarning(
                    "DJEN: {DiscardedCount} de {TotalCount} itens descartados por falta de campos obrigatórios ({Criterio}, página {Page}).",
                    discarded, payload.Items!.Count, query.Describe(), page);
            }

            var resultPage = new CnjPublicationPage(items, page, pageSize, payload.Count);

            logger.LogInformation(
                "DJEN: {ItemCount} comunicações recebidas ({Criterio}, página {Page} de {TotalCount} itens).",
                items.Count, query.Describe(), page, payload.Count);

            if (resultPage.HasSuspiciousTotalCount)
            {
                // O DJEN repete o tamanho da página no lugar do total quando
                // itensPorPagina=100 (ver CnjPublicationPage.HasMorePages). A paginação não
                // depende do count para estar correta, mas registrar o caso permite detectar
                // se o comportamento da API mudar.
                logger.LogDebug(
                    "DJEN: total declarado ({TotalCount}) é igual ao tamanho da página — provavelmente não é o total real. A paginação segue pela contagem de itens.",
                    payload.Count);
            }

            return Result.Success(resultPage);
        }
    }

    public async Task<Result> PingAsync(CancellationToken cancellationToken = default)
    {
        // Consulta mínima (1 item, um único dia) só para exercitar rota + disponibilidade.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await GetPublicationsAsync(CnjPublicationQuery.ByPeriod(today, today, pageSize: 1), cancellationToken);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    private string BuildRequestUri(CnjPublicationQuery query, int page, int pageSize)
    {
        var parameters = new List<string>
        {
            $"pagina={page.ToString(CultureInfo.InvariantCulture)}",
            $"itensPorPagina={pageSize.ToString(CultureInfo.InvariantCulture)}",
        };

        if (query.StartDate is { } startDate)
        {
            parameters.Add($"dataDisponibilizacaoInicio={startDate:yyyy-MM-dd}");
        }

        if (query.EndDate is { } endDate)
        {
            parameters.Add($"dataDisponibilizacaoFim={endDate:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(query.Tribunal))
        {
            parameters.Add($"siglaTribunal={Uri.EscapeDataString(query.Tribunal)}");
        }

        if (!string.IsNullOrWhiteSpace(query.CnjNumber))
        {
            // O DJEN espera o número SEM máscara. Enviar "1234567-89.2024.8.26.0001" devolve
            // zero resultados silenciosamente — não é erro, é só uma consulta que não casa
            // com nada, o que tornaria a falha invisível.
            parameters.Add($"numeroProcesso={Uri.EscapeDataString(NonDigitsRegex().Replace(query.CnjNumber, string.Empty))}");
        }

        return $"{_options.PublicationsPath}?{string.Join('&', parameters)}";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitsRegex();

    private Error MapStatusCode(HttpStatusCode statusCode, CnjPublicationQuery query, int page)
    {
        logger.LogWarning(
            "DJEN respondeu {StatusCode} ({Criterio}, página {Page}).", (int)statusCode, query.Describe(), page);

        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => ErrorFactory.From(ErrorCode.CNJ_RATE_LIMITED),
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ErrorFactory.From(ErrorCode.CNJ_TIMEOUT),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorFactory.From(ErrorCode.CNJ_AUTH_FAILED),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ErrorFactory.From(ErrorCode.CNJ_INVALID_REQUEST),
            _ => ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE),
        };
    }

    /// <summary>
    /// Traduz um item do DJEN para o DTO da Application. Devolve null (e o chamador
    /// descarta com log) quando faltar algum campo sem o qual a publicação não pode ser
    /// persistida: id, número do processo, texto ou data de disponibilização. Descartar é
    /// preferível a relançar — um item malformado no meio da página não deve abortar a
    /// captura das outras centenas.
    /// </summary>
    private static CnjPublicationDto? MapItem(DjenItem item)
    {
        var cnjNumber = item.NumeroProcessoComMascara ?? item.NumeroProcesso;

        if (item.Id == 0
            || string.IsNullOrWhiteSpace(cnjNumber)
            || string.IsNullOrWhiteSpace(item.Texto)
            || !TryParsePublishedAt(item.DataDisponibilizacao, out var publishedAt))
        {
            return null;
        }

        return new CnjPublicationDto(
            ExternalId: item.Id.ToString(CultureInfo.InvariantCulture),
            CnjNumber: cnjNumber,
            Tribunal: item.SiglaTribunal,
            CommunicationType: item.TipoComunicacao,
            RawContent: item.Texto,
            PublishedAt: publishedAt,
            MetadataJson: JsonSerializer.Serialize(
                new
                {
                    hash = item.Hash,
                    orgao = item.NomeOrgao,
                    classe = item.NomeClasse,
                    meio = item.Meio,
                    link = item.Link,
                    tipoDocumento = item.TipoDocumento,
                    numeroComunicacao = item.NumeroComunicacao,
                    numeroProcessoSemMascara = item.NumeroProcesso,
                },
                SerializerOptions));
    }

    /// <summary>
    /// O DJEN publica a data de disponibilização como "yyyy-MM-dd" (sem hora nem fuso).
    /// Interpretamos como meia-noite UTC — é a única leitura determinística possível, e a
    /// hora exata não é usada em nenhuma regra (prazos são contados por dia útil).
    /// </summary>
    private static bool TryParsePublishedAt(string? value, out DateTimeOffset publishedAt)
    {
        publishedAt = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            publishedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            publishedAt = parsed.ToUniversalTime();
            return true;
        }

        return false;
    }
}

internal static class DjenContentExtensions
{
    /// <summary>
    /// Lê o corpo como <see cref="DjenResponse"/> tolerando corpo vazio (204/200 sem
    /// conteúdo), que o CNJ devolve ocasionalmente em consultas sem resultado.
    /// </summary>
    internal static async Task<DjenResponse?> ReadFromJsonSafeAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        var json = await content.ReadAsStringAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? new DjenResponse { Count = 0, Items = [] }
            : JsonSerializer.Deserialize<DjenResponse>(json, DjenClient.DeserializerOptions);
    }
}
