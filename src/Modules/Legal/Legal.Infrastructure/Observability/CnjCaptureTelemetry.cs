using System.Diagnostics;
using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Modules.Legal.Infrastructure.Observability;

/// <summary>
/// Spans e métricas da captura de publicações do CNJ (ver ADR-036).
///
/// Usa o Meter "Advocacia" (<see cref="MeterNames.Advocacia"/>) em vez de criar um próprio:
/// o OpenTelemetry só coleta Meters explicitamente registrados em
/// OpenTelemetryConfiguration, e instrumentos de qualquer Meter com esse nome são coletados
/// juntos — assim um módulo novo não precisa alterar a configuração central.
///
/// Nenhuma métrica ou atributo de span carrega o conteúdo da publicação. Os atributos são
/// só contadores, tribunal, página e datas (ver claude.md, seção 14).
/// </summary>
public sealed class CnjCaptureTelemetry : IDisposable
{
    public const string ActivitySourceName = "Advocacia.CNJ.Capture";

    public const string JobSpanName = "cnj.capture.job";
    public const string ProcessSpanName = "cnj.capture.process";
    public const string PageSpanName = "cnj.capture.page";
    public const string PublicationSpanName = "cnj.capture.publication";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly Meter _meter = new(MeterNames.Advocacia);
    private readonly Counter<int> _publicationsCounter;
    private readonly Counter<int> _duplicatesCounter;
    private readonly Counter<int> _invalidCounter;
    private readonly Counter<int> _errorsCounter;
    private readonly Histogram<double> _duration;

    public CnjCaptureTelemetry()
    {
        _publicationsCounter = _meter.CreateCounter<int>(
            "cnj.capture.publications.count", description: "Publicações novas persistidas pela captura do CNJ.");
        _duplicatesCounter = _meter.CreateCounter<int>(
            "cnj.capture.duplicates.count", description: "Publicações ignoradas por já existirem (deduplicação por ExternalId).");
        _invalidCounter = _meter.CreateCounter<int>(
            "cnj.capture.invalid.count", description: "Publicações rejeitadas por dado inválido (CNJ, data ou conteúdo).");
        _errorsCounter = _meter.CreateCounter<int>(
            "cnj.capture.errors.count", description: "Falhas durante a captura (indisponibilidade do CNJ, erro de persistência).");
        _duration = _meter.CreateHistogram<double>(
            "cnj.capture.duration", unit: "ms", description: "Duração de uma execução completa do job de captura.");
    }

    public static Activity? StartJobSpan() => Source.StartActivity(JobSpanName, ActivityKind.Internal);

    /// <summary>
    /// Span de uma página. O critério (tribunal ou número de processo) entra como ATRIBUTO
    /// de span, não como tag de métrica: número de processo é alta cardinalidade e
    /// explodiria as séries temporais (ver <see cref="Add"/>).
    /// </summary>
    /// <summary>Span de um alvo de captura (um processo cadastrado ou um tribunal).</summary>
    public static Activity? StartProcessSpan(string criterio)
    {
        var activity = Source.StartActivity(ProcessSpanName, ActivityKind.Internal);
        activity?.SetTag("cnj.criterio", criterio);

        return activity;
    }

    public static Activity? StartPageSpan(string criterio, int page)
    {
        var activity = Source.StartActivity(PageSpanName, ActivityKind.Internal);
        activity?.SetTag("cnj.criterio", criterio);
        activity?.SetTag("cnj.page", page);

        return activity;
    }

    public static Activity? StartPublicationSpan(string externalId)
    {
        var activity = Source.StartActivity(PublicationSpanName, ActivityKind.Internal);
        activity?.SetTag("cnj.external_id", externalId);

        return activity;
    }

    public void RecordCaptured(int count, CaptureMode mode, string? tribunal) => Add(_publicationsCounter, count, mode, tribunal);

    public void RecordDuplicates(int count, CaptureMode mode, string? tribunal) => Add(_duplicatesCounter, count, mode, tribunal);

    public void RecordInvalid(int count, CaptureMode mode, string? tribunal) => Add(_invalidCounter, count, mode, tribunal);

    public void RecordErrors(int count, CaptureMode mode, string? tribunal) => Add(_errorsCounter, count, mode, tribunal);

    public void RecordDuration(TimeSpan duration) => _duration.Record(duration.TotalMilliseconds);

    /// <summary>
    /// Tags de BAIXA cardinalidade apenas: <c>modo</c> (dois valores) e <c>tribunal</c>
    /// (algumas dezenas). O número do processo NUNCA vira tag de métrica — com milhares de
    /// processos acompanhados, cada um viraria uma série temporal própria, inflando o custo
    /// de ingestão do Application Insights e tornando as métricas inúteis para agregação.
    /// Esse detalhe vive nos spans e nos logs, que são feitos para alta cardinalidade.
    /// </summary>
    private static void Add(Counter<int> counter, int count, CaptureMode mode, string? tribunal)
    {
        if (count <= 0)
        {
            return;
        }

        counter.Add(
            count,
            new KeyValuePair<string, object?>("modo", mode == CaptureMode.Processo ? "processo" : "tribunal"),
            new KeyValuePair<string, object?>("tribunal", tribunal ?? "-"));
    }

    public void Dispose() => _meter.Dispose();
}

/// <summary>Como a captura chegou até a publicação — ver CnjCaptureJob.</summary>
public enum CaptureMode
{
    /// <summary>Varredura do diário por período e tribunal.</summary>
    Tribunal,

    /// <summary>Consulta dirigida ao histórico de um processo acompanhado.</summary>
    Processo,
}
