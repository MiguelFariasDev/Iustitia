using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Implementação real de <see cref="IAppMetrics"/> — instrumentos do Meter "Advocacia"
/// (ver MeterNames), coletados pelo OpenTelemetryConfiguration e exportados via /metrics
/// (Prometheus) e Application Insights (ver ADR-036).
/// </summary>
public sealed class CustomMetrics : IAppMetrics
{
    private readonly Meter _meter = new(MeterNames.Advocacia);
    private int _outboxPendingCount;

    private readonly Counter<int> _outboxProcessedCounter;
    private readonly Counter<int> _outboxFailedCounter;
    private readonly Histogram<double> _outboxProcessingDuration;
    private readonly Counter<int> _jobsExecutedCounter;
    private readonly Counter<int> _jobsFailedCounter;
    private readonly Histogram<double> _jobsDuration;
    private readonly Counter<int> _consumersProcessedCounter;
    private readonly Counter<int> _consumersFailedCounter;
    private readonly Counter<int> _handlersExecutedCounter;
    private readonly Histogram<double> _handlersDuration;

    public CustomMetrics()
    {
        _meter.CreateObservableGauge("outbox.pending.count", () => _outboxPendingCount, description: "Mensagens pendentes na outbox.");

        _outboxProcessedCounter = _meter.CreateCounter<int>("outbox.processed.count", description: "Mensagens da outbox publicadas com sucesso.");
        _outboxFailedCounter = _meter.CreateCounter<int>("outbox.failed.count", description: "Mensagens da outbox que falharam ao publicar.");
        _outboxProcessingDuration = _meter.CreateHistogram<double>("outbox.processing.duration", unit: "ms", description: "Duração de um ciclo de processamento da outbox.");

        _jobsExecutedCounter = _meter.CreateCounter<int>("jobs.executed.count", description: "Execuções de jobs recorrentes.");
        _jobsFailedCounter = _meter.CreateCounter<int>("jobs.failed.count", description: "Execuções de jobs recorrentes que falharam.");
        _jobsDuration = _meter.CreateHistogram<double>("jobs.duration", unit: "ms", description: "Duração de execução de um job recorrente.");

        _consumersProcessedCounter = _meter.CreateCounter<int>("consumers.processed.count", description: "Mensagens processadas com sucesso por consumers.");
        _consumersFailedCounter = _meter.CreateCounter<int>("consumers.failed.count", description: "Mensagens que falharam ao serem processadas por consumers.");

        _handlersExecutedCounter = _meter.CreateCounter<int>("handlers.executed.count", description: "Commands/Queries executados via MediatR.");
        _handlersDuration = _meter.CreateHistogram<double>("handlers.duration", unit: "ms", description: "Duração de execução de um Command/Query.");
    }

    public void RecordOutboxPendingCount(int count) => _outboxPendingCount = count;

    public void RecordOutboxProcessed(int count) => _outboxProcessedCounter.Add(count);

    public void RecordOutboxFailed(int count) => _outboxFailedCounter.Add(count);

    public void RecordOutboxProcessingDuration(TimeSpan duration) => _outboxProcessingDuration.Record(duration.TotalMilliseconds);

    public void RecordJobExecuted(string jobName, TimeSpan duration, bool success)
    {
        var tag = new KeyValuePair<string, object?>("job", jobName);
        _jobsExecutedCounter.Add(1, tag);
        _jobsDuration.Record(duration.TotalMilliseconds, tag);

        if (!success)
        {
            _jobsFailedCounter.Add(1, tag);
        }
    }

    public void RecordConsumerProcessed(string consumerName, bool success)
    {
        var tag = new KeyValuePair<string, object?>("consumer", consumerName);

        if (success)
        {
            _consumersProcessedCounter.Add(1, tag);
        }
        else
        {
            _consumersFailedCounter.Add(1, tag);
        }
    }

    public void RecordHandlerExecuted(string requestName, TimeSpan duration, bool success)
    {
        var tag = new KeyValuePair<string, object?>("handler", requestName);
        _handlersExecutedCounter.Add(1, tag);
        _handlersDuration.Record(duration.TotalMilliseconds, tag);
    }
}
