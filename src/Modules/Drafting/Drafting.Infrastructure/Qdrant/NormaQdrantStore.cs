using System.Globalization;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// Banco vetorial do acervo normativo PÚBLICO: uma coleção por tipo de conteúdo, vetores BGE-M3 em disco,
/// quantização escalar int8 (vetores quantizados ficam na RAM; originais no disco) e índices de payload
/// para todos os filtros. Sem filtro de tenant — o acervo é compartilhado e nunca contém dado de cliente;
/// documentos de cliente ficam em <see cref="QdrantService"/>, que SEMPRE filtra por tenant.
/// </summary>
public sealed class NormaQdrantStore : INormaVectorStore, IDisposable
{
    private static readonly string[] EsferasNaoEstaduais = ["federal", "regulador", "jurisprudencia", "internacional"];
    private static readonly string[] EsferasNaoMunicipais = ["federal", "estadual", "regulador", "jurisprudencia", "internacional"];

    private readonly QdrantClient _client;
    private readonly ILogger<NormaQdrantStore> _logger;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _ensured;

    public NormaQdrantStore(IOptions<QdrantOptions> options, ILogger<NormaQdrantStore> logger)
    {
        _logger = logger;
        var settings = options.Value;
        var problem = settings.Validate();
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        var uri = new Uri(settings.BaseUrl);
        _client = new QdrantClient(
            uri.Host,
            uri.Port,
            uri.Scheme == Uri.UriSchemeHttps,
            string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey,
            TimeSpan.FromSeconds(settings.TimeoutSegundos));
    }

    public async Task EnsureCollectionsAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        if (_ensured)
        {
            return;
        }

        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            if (_ensured)
            {
                return;
            }

            await Guarded(async () =>
            {
                foreach (var colecao in Colecoes.Publicas.Append(Colecoes.ModelosDePeca))
                {
                    await EnsureOneAsync(colecao, dimensions, cancellationToken);
                }

                return 0;
            });
            _ensured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    private async Task EnsureOneAsync(string colecao, int dimensions, CancellationToken cancellationToken)
    {
        if (await _client.CollectionExistsAsync(colecao, cancellationToken))
        {
            return;
        }

        await _client.CreateCollectionAsync(
            colecao,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine, OnDisk = true },
            onDiskPayload: true,
            quantizationConfig: new QuantizationConfig
            {
                Scalar = new ScalarQuantization { Type = QuantizationType.Int8, Quantile = 0.99f, AlwaysRam = true },
            },
            cancellationToken: cancellationToken);

        foreach (var campo in new[]
                 {
                     "normaJuridicaId", "urn", "tipo", "numero", "situacao", "versaoTexto", "esfera", "uf", "municipio",
                     "area", "tribunal", "assuntos", "artigo", "hashConteudo",
                 })
        {
            await _client.CreatePayloadIndexAsync(colecao, campo, PayloadSchemaType.Keyword, cancellationToken: cancellationToken);
        }

        await _client.CreatePayloadIndexAsync(colecao, "revogada", PayloadSchemaType.Bool, cancellationToken: cancellationToken);
        await _client.CreatePayloadIndexAsync(colecao, "versao", PayloadSchemaType.Integer, cancellationToken: cancellationToken);
        await _client.CreatePayloadIndexAsync(colecao, "data", PayloadSchemaType.Datetime, cancellationToken: cancellationToken);

        _logger.LogInformation("Qdrant: coleção '{Colecao}' criada ({Dim} dimensões, int8, vetores em disco).", colecao, dimensions);
    }

    public Task UpsertAsync(string colecao, IReadOnlyList<NormaVectorPoint> points, CancellationToken cancellationToken = default) =>
        Guarded(async () =>
        {
            const int batch = 64;
            for (var offset = 0; offset < points.Count; offset += batch)
            {
                var pontos = points.Skip(offset).Take(batch).Select(ToPoint).ToList();
                await _client.UpsertAsync(colecao, pontos, wait: true, cancellationToken: cancellationToken);
            }

            return 0;
        });

    public Task DeleteAsync(string colecao, Guid normaId, int? manterVersao, CancellationToken cancellationToken = default) =>
        Guarded(async () =>
        {
            if (!await _client.CollectionExistsAsync(colecao, cancellationToken))
            {
                return 0;
            }

            var filter = new Filter();
            filter.Must.Add(Conditions.MatchKeyword("normaJuridicaId", normaId.ToString()));
            if (manterVersao is { } versao)
            {
                filter.MustNot.Add(Conditions.Match("versao", (long)versao));
            }

            await _client.DeleteAsync(colecao, filter, wait: true, cancellationToken: cancellationToken);

            return 0;
        });

    public Task<IReadOnlyList<VectorHit>> SearchAsync(
        NormaFiltro filtro, float[] vetor, int limit, CancellationToken cancellationToken = default) =>
        Guarded(async () =>
        {
            var qdrantFilter = BuildFilter(filtro);
            var hits = new List<VectorHit>();

            // Coleções são independentes: busca em todas as pedidas e funde pelo score (mesma métrica: cosseno).
            foreach (var colecao in filtro.ColecoesEfetivas)
            {
                try
                {
                    var results = await _client.SearchAsync(
                        colecao,
                        vetor,
                        filter: qdrantFilter,
                        limit: (ulong)Math.Max(1, limit),
                        payloadSelector: new WithPayloadSelector { Enable = true },
                        cancellationToken: cancellationToken);
                    hits.AddRange(results.Select(ToHit));
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    // Coleção ainda não criada (nada ingerido nesse tipo): sem resultados, não é erro.
                }
            }

            return (IReadOnlyList<VectorHit>)hits.OrderByDescending(h => h.Score).Take(limit).ToList();
        });

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

    public void Dispose()
    {
        _client.Dispose();
        _ensureLock.Dispose();
    }

    private async Task<T> Guarded<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (RpcException exception)
        {
            _logger.LogWarning("Qdrant (acervo): falha gRPC ({Status}).", exception.StatusCode);
            throw new VectorStoreException($"O Qdrant respondeu {exception.StatusCode}.", exception);
        }
    }

    private async Task Guarded(Func<Task<int>> action) => await Guarded<int>(action);

    internal static Filter BuildFilter(NormaFiltro f)
    {
        var filter = new Filter();

        if (!f.IncluirRevogadas)
        {
            filter.Must.Add(Conditions.Match("revogada", false));
        }

        AddAny(filter, "tipo", f.Tipos);
        AddAny(filter, "tribunal", f.Tribunais?.Select(t => t.ToUpperInvariant()).ToList());
        AddAny(filter, "esfera", f.Esferas);
        AddAny(filter, "assuntos", f.Assuntos);

        if (f.NormaIds is { Count: > 0 } ids)
        {
            filter.Must.Add(Conditions.Match("normaJuridicaId", ids.Select(i => i.ToString()).ToList()));
        }

        if (f.Ufs is { Count: > 0 } ufs)
        {
            // Restringe só o que TEM UF (estadual/municipal); federal, tribunais e reguladores continuam valendo.
            var uf = new Filter();
            uf.Should.Add(Conditions.Match("uf", ufs.Select(u => u.ToUpperInvariant()).ToList()));
            uf.Should.Add(Conditions.Match("esfera", EsferasNaoEstaduais.ToList()));
            filter.Must.Add(Conditions.Filter(uf));
        }

        if (f.Municipios is { Count: > 0 } municipios)
        {
            var municipio = new Filter();
            municipio.Should.Add(Conditions.Match("municipio", municipios.ToList()));
            municipio.Should.Add(Conditions.Match("esfera", EsferasNaoMunicipais.ToList()));
            filter.Must.Add(Conditions.Filter(municipio));
        }

        if (f.AreaEstrita && !string.IsNullOrWhiteSpace(f.Area))
        {
            filter.Must.Add(Conditions.MatchKeyword("area", f.Area));
        }

        if (f.DataDe is not null || f.DataAte is not null)
        {
            filter.Must.Add(Conditions.DatetimeRange(
                "data",
                gte: f.DataDe?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lte: f.DataAte?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)));
        }

        return filter;
    }

    private static void AddAny(Filter filter, string key, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            filter.Must.Add(Conditions.Match(key, values.ToList()));
        }
    }

    internal static PointStruct ToPoint(NormaVectorPoint point)
    {
        var p = point.Payload;
        var result = new PointStruct { Id = point.Id, Vectors = point.Vector };

        result.Payload["normaJuridicaId"] = Text(p.NormaJuridicaId.ToString());
        result.Payload["titulo"] = Text(p.Titulo);
        result.Payload["tipo"] = Text(p.Tipo);
        result.Payload["situacao"] = Text(p.Situacao);
        result.Payload["versaoTexto"] = Text(p.VersaoTexto);
        result.Payload["esfera"] = Text(p.Esfera);
        result.Payload["colecao"] = Text(p.Colecao);
        result.Payload["arquivoMarkdownPath"] = Text(p.ArquivoMarkdownPath);
        result.Payload["fonteOficialUrl"] = Text(p.FonteOficialUrl);
        result.Payload["hashConteudo"] = Text(p.HashConteudo);
        result.Payload["texto"] = Text(p.Texto);
        result.Payload["revogada"] = new Value { BoolValue = p.Revogada };
        result.Payload["versao"] = new Value { IntegerValue = p.VersaoNumero };
        SetIf(result, "urn", p.Urn);
        SetIf(result, "numero", p.Numero);
        SetIf(result, "uf", p.Uf);
        SetIf(result, "municipio", p.Municipio);
        SetIf(result, "area", p.Area);
        SetIf(result, "tribunal", p.Tribunal?.ToUpperInvariant());
        SetIf(result, "secao", p.Secao);
        SetIf(result, "artigo", p.Artigo);
        SetIf(result, "paragrafo", p.Paragrafo);
        SetIf(result, "inciso", p.Inciso);

        if (p.Pagina is { } pagina)
        {
            result.Payload["pagina"] = new Value { IntegerValue = pagina };
        }

        if (p.Data is { } data)
        {
            result.Payload["data"] = Text(
                data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }

        if (p.Assuntos.Count > 0)
        {
            var list = new ListValue();
            foreach (var assunto in p.Assuntos)
            {
                list.Values.Add(Text(assunto));
            }

            result.Payload["assuntos"] = new Value { ListValue = list };
        }

        return result;
    }

    internal static VectorHit ToHit(ScoredPoint point)
    {
        var payload = point.Payload;
        string? Get(string key) => payload.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.StringValue ? v.StringValue : null;
        long? GetInt(string key) => payload.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.IntegerValue ? v.IntegerValue : null;
        bool GetBool(string key) => payload.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.BoolValue && v.BoolValue;

        DateOnly? data = DateTime.TryParse(
            Get("data"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;

        IReadOnlyList<string> assuntos = payload.TryGetValue("assuntos", out var a) && a.KindCase == Value.KindOneofCase.ListValue
            ? a.ListValue.Values.Where(v => v.KindCase == Value.KindOneofCase.StringValue).Select(v => v.StringValue).ToList()
            : [];

        var normaPayload = new NormaPayload(
            Guid.TryParse(Get("normaJuridicaId"), out var normaId) ? normaId : Guid.Empty,
            Get("urn"),
            Get("titulo") ?? "norma",
            Get("tipo") ?? "Norma",
            Get("numero"),
            data,
            Get("situacao") ?? "Não informada",
            GetBool("revogada"),
            Get("versaoTexto") ?? "Não informada",
            (int)(GetInt("versao") ?? 1),
            Get("esfera") ?? "federal",
            Get("uf"),
            Get("municipio"),
            Get("area"),
            Get("tribunal"),
            assuntos,
            Get("colecao") ?? string.Empty,
            Get("arquivoMarkdownPath") ?? string.Empty,
            Get("fonteOficialUrl") ?? string.Empty,
            (int?)GetInt("pagina"),
            Get("secao"),
            Get("artigo"),
            Get("paragrafo"),
            Get("inciso"),
            Get("hashConteudo") ?? string.Empty,
            Get("texto") ?? string.Empty);

        return normaPayload.ToHit(point.Id.Uuid, point.Score);
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
