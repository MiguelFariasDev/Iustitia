using Advocacia.Modules.Drafting.Application.Abstractions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>O cliente gRPC (thread-safe e caro de criar: um por processo) e o tratamento comum de falha.</summary>
public sealed class QdrantConexao : IDisposable
{
    /// <summary>Vetor BGE-M3.</summary>
    public const string Denso = "denso";

    /// <summary>Vetor esparso do BM25; o IDF é calculado pelo Qdrant.</summary>
    public const string Esparso = "bm25";

    private readonly ILogger<QdrantConexao> _logger;

    public QdrantConexao(IOptions<QdrantOptions> options, ILogger<QdrantConexao> logger)
    {
        _logger = logger;
        Opcoes = options.Value;
        if (Opcoes.Validate() is { } problema)
        {
            throw new InvalidOperationException(problema);
        }

        var uri = new Uri(Opcoes.BaseUrl);
        Cliente = new QdrantClient(
            uri.Host,
            uri.Port,
            uri.Scheme == Uri.UriSchemeHttps,
            string.IsNullOrWhiteSpace(Opcoes.ApiKey) ? null : Opcoes.ApiKey,
            TimeSpan.FromSeconds(Opcoes.TimeoutSegundos));
    }

    internal QdrantClient Cliente { get; }

    internal QdrantOptions Opcoes { get; }

    public void Dispose() => Cliente.Dispose();

    /// <summary>Falha de rede/gRPC vira <see cref="VectorStoreException"/>: transitória, quem chamou repete.</summary>
    internal async Task<T> ExecutarAsync<T>(Func<QdrantClient, Task<T>> acao)
    {
        try
        {
            return await acao(Cliente);
        }
        catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled)
        {
            _logger.LogWarning("Qdrant: falha gRPC ({Status}).", ex.StatusCode);
            throw new VectorStoreException($"O Qdrant respondeu {ex.StatusCode}.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new VectorStoreException("Não foi possível conectar ao Qdrant.", ex);
        }
    }

    /// <summary>Busca numa coleção. Coleção ainda não criada (nada ingerido) é lista vazia, não erro.</summary>
    internal async Task<IReadOnlyList<ScoredPoint>> BuscarAsync(
        string colecao, Query consulta, string vetor, Filter filtro, int limite, CancellationToken ct)
    {
        try
        {
            return await Cliente.QueryAsync(
                colecao,
                query: consulta,
                usingVector: vetor,
                filter: filtro,
                limit: (ulong)Math.Max(1, limite),
                payloadSelector: true,
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled)
        {
            _logger.LogWarning("Qdrant: falha gRPC ({Status}) ao buscar em {Colecao}.", ex.StatusCode, colecao);
            throw new VectorStoreException($"O Qdrant respondeu {ex.StatusCode}.", ex);
        }
    }

    internal static Vectors Vetores(float[] denso, Application.Busca.VetorEsparso esparso)
    {
        var vetores = new Dictionary<string, Vector> { [Denso] = denso };
        if (!esparso.Vazio)
        {
            vetores[Esparso] = (esparso.Valores, esparso.Indices);
        }

        return vetores;
    }

    internal static Value Texto(string valor) => new() { StringValue = valor };

    internal static void Definir(PointStruct ponto, string chave, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            ponto.Payload[chave] = Texto(valor);
        }
    }
}

/// <summary>Leitura tolerante do payload: campo ausente ou de outro tipo é nulo, nunca exceção.</summary>
internal static class PayloadExtensions
{
    public static string? Str(this IDictionary<string, Value> payload, string chave) =>
        payload.TryGetValue(chave, out var v) && v.KindCase == Value.KindOneofCase.StringValue ? v.StringValue : null;

    public static long? Int(this IDictionary<string, Value> payload, string chave) =>
        payload.TryGetValue(chave, out var v) && v.KindCase == Value.KindOneofCase.IntegerValue ? v.IntegerValue : null;

    public static double? Num(this IDictionary<string, Value> payload, string chave) =>
        payload.TryGetValue(chave, out var v) && v.KindCase == Value.KindOneofCase.DoubleValue ? v.DoubleValue : null;

    public static bool Bool(this IDictionary<string, Value> payload, string chave) =>
        payload.TryGetValue(chave, out var v) && v.KindCase == Value.KindOneofCase.BoolValue && v.BoolValue;

    public static Guid? Id(this IDictionary<string, Value> payload, string chave) =>
        Guid.TryParse(payload.Str(chave), out var id) ? id : null;

    public static DateOnly? Data(this IDictionary<string, Value> payload, string chave) =>
        DateTime.TryParse(
            payload.Str(chave), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var data)
            ? DateOnly.FromDateTime(data)
            : null;

    /// <summary>RFC 3339: é o formato que o índice "datetime" do Qdrant entende.</summary>
    public static string Rfc3339(this DateOnly data) =>
        data.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00Z";
}
