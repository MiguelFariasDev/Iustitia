using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;
using Serilog;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class CorrelationIdEnricherTests
{
    private static LogEvent CaptureLogEvent(Action beforeLog)
    {
        LogEvent? captured = null;
        using var logger = new LoggerConfiguration()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Sink(new DelegatingSink(logEvent => captured = logEvent))
            .CreateLogger();

        beforeLog();
        logger.Information("mensagem de teste");

        return captured!;
    }

    [Fact]
    public void Enrich_WhenCorrelationIdIsSet_AddsProperty()
    {
        var correlationId = Guid.NewGuid();

        var logEvent = CaptureLogEvent(() => LogEnrichmentContext.CorrelationId = correlationId);

        logEvent.Properties.Should().ContainKey("CorrelationId");
        logEvent.Properties["CorrelationId"].ToString().Should().Contain(correlationId.ToString());

        LogEnrichmentContext.CorrelationId = null;
    }

    [Fact]
    public void Enrich_WhenCorrelationIdIsNotSet_DoesNotAddProperty()
    {
        LogEnrichmentContext.CorrelationId = null;

        var logEvent = CaptureLogEvent(() => { });

        logEvent.Properties.Should().NotContainKey("CorrelationId");
    }

    private sealed class DelegatingSink(Action<LogEvent> onEmit) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => onEmit(logEvent);
    }
}
