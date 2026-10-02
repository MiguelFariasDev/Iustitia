using System.Diagnostics;
using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Spans e métricas das consultas ao DataJud (ver ADR-036 e ADR-064).
///
/// Nenhum atributo carrega dado do processo além do tribunal: número de processo é
/// identificador de um caso real e não entra em métrica, que é agregada e retida por muito
/// mais tempo que um log.
/// </summary>
public sealed class DataJudTelemetry : IDisposable
{
    public const string ActivitySourceName = "Advocacia.CNJ.DataJud";

    public const string ConsultarSpanName = "datajud.consultar";
    public const string ParseSpanName = "datajud.parse";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly Meter _meter = new(MeterNames.Advocacia);
    private readonly Counter<int> _requests;
    private readonly Counter<int> _failed;
    private readonly Counter<int> _rateLimited;
    private readonly Counter<int> _notFound;
    private readonly Counter<int> _cacheHits;
    private readonly Histogram<double> _duration;

    public DataJudTelemetry()
    {
        _requests = _meter.CreateCounter<int>(
            "datajud.requests.count", description: "Consultas enviadas ao DataJud.");
        _failed = _meter.CreateCounter<int>(
            "datajud.requests.failed.count", description: "Consultas ao DataJud que falharam.");
        _rateLimited = _meter.CreateCounter<int>(
            "datajud.requests.rate_limited.count", description: "Consultas recusadas pelo DataJud por excesso de requisições.");
        _notFound = _meter.CreateCounter<int>(
            "datajud.requests.not_found.count", description: "Consultas em que o processo não existe no DataJud.");
        _cacheHits = _meter.CreateCounter<int>(
            "datajud.cache.hits.count", description: "Consultas respondidas pelo cache, sem tocar o DataJud.");
        _duration = _meter.CreateHistogram<double>(
            "datajud.requests.duration", unit: "ms", description: "Latência das consultas ao DataJud.");
    }

    public static Activity? StartConsulta(string tribunal)
    {
        var activity = Source.StartActivity(ConsultarSpanName, ActivityKind.Client);
        activity?.SetTag("datajud.tribunal", tribunal);
        return activity;
    }

    public static Activity? StartParse() => Source.StartActivity(ParseSpanName);

    public void RecordRequest(string tribunal) => _requests.Add(1, Tag(tribunal));

    public void RecordFailure(string tribunal, string errorCode) =>
        _failed.Add(1, Tag(tribunal), new KeyValuePair<string, object?>("datajud.error", errorCode));

    public void RecordRateLimited(string tribunal) => _rateLimited.Add(1, Tag(tribunal));

    public void RecordNotFound(string tribunal) => _notFound.Add(1, Tag(tribunal));

    public void RecordCacheHit(string tribunal) => _cacheHits.Add(1, Tag(tribunal));

    public void RecordDuration(string tribunal, double milliseconds) => _duration.Record(milliseconds, Tag(tribunal));

    private static KeyValuePair<string, object?> Tag(string tribunal) => new("datajud.tribunal", tribunal);

    public void Dispose() => _meter.Dispose();
}
