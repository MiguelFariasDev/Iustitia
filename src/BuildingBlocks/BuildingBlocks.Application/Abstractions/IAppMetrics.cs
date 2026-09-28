namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Fachada de métricas customizadas (ver ADR-036), para uso a partir da Application layer
/// (ex.: MetricsBehavior do MediatR) sem depender de OpenTelemetry/System.Diagnostics.Metrics
/// diretamente — a implementação real (baseada em <c>System.Diagnostics.Metrics.Meter</c>)
/// fica em BuildingBlocks.Infrastructure.Observability.
/// </summary>
public interface IAppMetrics
{
    void RecordHandlerExecuted(string requestName, TimeSpan duration, bool success);

    /// <summary>Atualiza o valor do gauge outbox.pending.count — chamado periodicamente (ver HealthCheckJob).</summary>
    void RecordOutboxPendingCount(int count);

    void RecordOutboxProcessed(int count);

    void RecordOutboxFailed(int count);

    void RecordOutboxProcessingDuration(TimeSpan duration);

    void RecordJobExecuted(string jobName, TimeSpan duration, bool success);

    void RecordConsumerProcessed(string consumerName, bool success);
}
