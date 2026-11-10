using System.Globalization;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// Implementação de <see cref="IVectorStore"/> sobre o cliente oficial do Qdrant (gRPC).
///
/// ISOLAMENTO ENTRE ESCRITÓRIOS: o Qdrant não tem RLS. Todo ponto leva <c>tenantId</c> no payload e
/// TODA busca e exclusão passa por <see cref="TenantFilter"/> — não há caminho de leitura sem ele.
/// </summary>
public sealed class QdrantService : IVectorStore, IDisposable
{
    public const string TenantKey = "tenantId";

    private readonly QdrantOptions _options;
    private readonly string _colecao;
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(IOptions<QdrantOptions> options, IOptions<RagOptions> rag, ILogger<QdrantService> logger)
    {
        _options = options.Value;
        _colecao = rag.Value.Colecao;
        _logger = logger;

        var problem = _options.Validate();
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        var uri = new Uri(_options.BaseUrl);
        _client = new QdrantClient(
            uri.Host,
            uri.Port,
            uri.Scheme == Uri.UriSchemeHttps,
            string.IsNullOrWhiteSpace(_options.ApiKey) ? null : _options.ApiKey,
            TimeSpan.FromSeconds(_options.TimeoutSegundos));
    }

    public Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default) =>
        Guarded(() => EnsureCollectionCoreAsync(dimensions, cancellationToken));

    private async Task EnsureCollectionCoreAsync(int dimensions, CancellationToken cancellationToken)
    {
        if (await _client.CollectionExistsAsync(_colecao, cancellationToken))
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _colecao,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);

        // Índices de payload: sem eles o filtro por tenant/área varre a coleção inteira.
        foreach (var field in new[] { TenantKey, "area", "tipoDocumento", "tribunal", "documentoOrigemId", "visibilidade", "usuarioId" })
        {
            await _client.CreatePayloadIndexAsync(
                _colecao, field, PayloadSchemaType.Keyword, cancellationToken: cancellationToken);
        }

        await _client.CreatePayloadIndexAsync(
            _colecao, "dataPublicacao", PayloadSchemaType.Datetime, cancellationToken: cancellationToken);

        _logger.LogInformation("Qdrant: coleção '{Colecao}' criada ({Dimensoes} dimensões, cosseno).", _colecao, dimensions);
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(
        VectorSearchQuery query, CancellationToken cancellationToken = default) =>
        Guarded(() => SearchCoreAsync(query, cancellationToken));

    private async Task<IReadOnlyList<VectorHit>> SearchCoreAsync(VectorSearchQuery query, CancellationToken cancellationToken)
    {
        var results = await _client.SearchAsync(
            _colecao,
            query.Vector,
            filter: BuildFilter(query),
            limit: (ulong)Math.Max(1, query.Limit),
            payloadSelector: new WithPayloadSelector { Enable = true },
            scoreThreshold: query.ScoreMinimo is { } min ? (float)min : null,
            cancellationToken: cancellationToken);

        return results.Select(ToHit).ToList();
    }

    public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default) =>
        Guarded(() => UpsertCoreAsync(points, cancellationToken));

    private async Task UpsertCoreAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken)
    {
        const int batchSize = 64;
        for (var offset = 0; offset < points.Count; offset += batchSize)
        {
            var batch = points.Skip(offset).Take(batchSize).Select(ToPoint).ToList();
            await _client.UpsertAsync(_colecao, batch, wait: true, cancellationToken: cancellationToken);
        }
    }

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default) =>
        Guarded(() => DeleteCoreAsync(tenantId, documentoOrigemId, cancellationToken));

    private async Task DeleteCoreAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken)
    {
        var filter = TenantFilter(tenantId);
        filter.Must.Add(Conditions.MatchKeyword("documentoOrigemId", documentoOrigemId.ToString()));

        // A coleção pode ainda não existir (primeira indexação): nada a apagar.
        if (!await _client.CollectionExistsAsync(_colecao, cancellationToken))
        {
            return;
        }

        await _client.DeleteAsync(_colecao, filter, wait: true, cancellationToken: cancellationToken);
    }

    public async Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.HealthAsync(cancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is RpcException or HttpRequestException or TimeoutException)
        {
            return false;
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>Falha de rede/gRPC do Qdrant vira <see cref="VectorStoreException"/>: transitória, a geração é repetida.</summary>
    private async Task Guarded(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (RpcException exception)
        {
            _logger.LogWarning("Qdrant: falha gRPC ({Status}).", exception.StatusCode);
            throw new VectorStoreException($"O Qdrant respondeu {exception.StatusCode}.", exception);
        }
    }

    private async Task<T> Guarded<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (RpcException exception)
        {
            _logger.LogWarning("Qdrant: falha gRPC ({Status}).", exception.StatusCode);
            throw new VectorStoreException($"O Qdrant respondeu {exception.StatusCode}.", exception);
        }
    }

    /// <summary>O filtro de tenant. Todo outro filtro é ACRESCENTADO a ele, nunca no lugar dele.</summary>
    internal static Filter TenantFilter(Guid tenantId)
    {
        var filter = new Filter();
        filter.Must.Add(Conditions.MatchKeyword(TenantKey, tenantId.ToString()));

        return filter;
    }

    internal static Filter BuildFilter(VectorSearchQuery query)
    {
        var filter = TenantFilter(query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.Area))
        {
            filter.Must.Add(Conditions.MatchKeyword("area", query.Area));
        }

        if (!string.IsNullOrWhiteSpace(query.TipoDocumento))
        {
            filter.Must.Add(Conditions.MatchKeyword("tipoDocumento", query.TipoDocumento));
        }

        if (!string.IsNullOrWhiteSpace(query.Tribunal))
        {
            filter.Must.Add(Conditions.MatchKeyword("tribunal", query.Tribunal));
        }

        // Controle de acesso: documento do escritório vale para todos; "privado" só para quem enviou.
        // Sem usuário informado, só o que é do escritório. Filtro ANINHADO (OU) dentro do MUST.
        var visibility = new Filter();
        visibility.Should.Add(Conditions.MatchKeyword("visibilidade", "escritorio"));
        if (query.UsuarioId is { } usuarioId)
        {
            visibility.Should.Add(Conditions.MatchKeyword("usuarioId", usuarioId.ToString()));
        }

        filter.Must.Add(Conditions.Filter(visibility));

        if (query.DocumentosIds is { Count: > 0 } documentos)
        {
            filter.Must.Add(Conditions.Match("documentoOrigemId", documentos.Select(id => id.ToString()).ToList()));
        }

        if (query.PublicadoDe is not null || query.PublicadoAte is not null)
        {
            filter.Must.Add(Conditions.DatetimeRange(
                "dataPublicacao",
                gte: query.PublicadoDe?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lte: query.PublicadoAte?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)));
        }

        return filter;
    }

    internal static PointStruct ToPoint(VectorPoint point)
    {
        var meta = point.Meta;
        var result = new PointStruct
        {
            Id = Guid.Parse(point.Id),
            Vectors = point.Vector,
        };

        result.Payload[TenantKey] = Text(point.TenantId.ToString());
        result.Payload["texto"] = Text(point.Texto);
        result.Payload["nomeArquivo"] = Text(meta.NomeArquivo);
        result.Payload["hashConteudo"] = Text(meta.HashConteudo);
        SetIf(result, "documentoOrigemId", meta.DocumentoOrigemId?.ToString());
        SetIf(result, "tipoDocumento", meta.TipoDocumento);
        SetIf(result, "area", meta.Area);
        SetIf(result, "tribunal", meta.Tribunal);
        SetIf(result, "urlOrigem", meta.UrlOrigem);
        SetIf(result, "visibilidade", meta.Visibilidade);
        SetIf(result, "usuarioId", meta.UsuarioId?.ToString());
        SetIf(result, "referencia", meta.Referencia);

        if (meta.Tags is { Count: > 0 } tags)
        {
            var list = new ListValue();
            foreach (var tag in tags)
            {
                list.Values.Add(Text(tag));
            }

            result.Payload["tags"] = new Value { ListValue = list };
        }

        if (meta.Pagina is { } pagina)
        {
            result.Payload["pagina"] = new Value { IntegerValue = pagina };
        }

        if (meta.DataPublicacao is { } data)
        {
            // RFC 3339: é o formato que o índice "datetime" do Qdrant entende.
            result.Payload["dataPublicacao"] = Text(
                data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }

        return result;
    }

    internal static VectorHit ToHit(ScoredPoint point)
    {
        var payload = point.Payload;
        string? Get(string key) => payload.TryGetValue(key, out var value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : null;

        var pagina = payload.TryGetValue("pagina", out var pageValue) && pageValue.KindCase == Value.KindOneofCase.IntegerValue
            ? (int?)pageValue.IntegerValue
            : null;

        Guid? documentoId = Guid.TryParse(Get("documentoOrigemId"), out var parsed) ? parsed : null;
        DateOnly? data = DateTime.TryParse(
            Get("dataPublicacao"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? DateOnly.FromDateTime(date)
            : null;

        IReadOnlyList<string>? tags = payload.TryGetValue("tags", out var tagsValue) && tagsValue.KindCase == Value.KindOneofCase.ListValue
            ? tagsValue.ListValue.Values.Where(v => v.KindCase == Value.KindOneofCase.StringValue).Select(v => v.StringValue).ToList()
            : null;

        var meta = new ChunkMetadata(
            documentoId,
            Get("nomeArquivo") ?? "documento",
            pagina,
            Get("tipoDocumento"),
            Get("area"),
            Get("tribunal"),
            data,
            Get("urlOrigem"),
            Get("hashConteudo") ?? string.Empty,
            tags,
            Guid.TryParse(Get("usuarioId"), out var usuario) ? usuario : null,
            Get("visibilidade"),
            Get("referencia"));

        return new VectorHit(point.Id.Uuid, Get("texto") ?? string.Empty, point.Score, meta);
    }

    private static Value Text(string value) => new() { StringValue = value };

    private static void SetIf(PointStruct point, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            point.Payload[key] = Text(value);
        }
    }
}
