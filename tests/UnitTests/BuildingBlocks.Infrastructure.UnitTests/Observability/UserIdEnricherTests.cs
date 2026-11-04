using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;
using Serilog;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class UserIdEnricherTests
{
    private static LogEvent CaptureLogEvent(Action beforeLog)
    {
        LogEvent? captured = null;
        using var logger = new LoggerConfiguration()
            .Enrich.With<UserIdEnricher>()
            .WriteTo.Sink(new DelegatingSink(logEvent => captured = logEvent))
            .CreateLogger();

        beforeLog();
        logger.Information("mensagem de teste");

        return captured!;
    }

    [Fact]
    public void Enrich_WhenUserIdIsSet_AddsProperty()
    {
        var userId = Guid.NewGuid();

        var logEvent = CaptureLogEvent(() => LogEnrichmentContext.UserId = userId);

        logEvent.Properties.Should().ContainKey("UserId");

        LogEnrichmentContext.UserId = null;
    }

    [Fact]
    public void Enrich_WhenUserIdIsNotSet_DoesNotAddProperty()
    {
        LogEnrichmentContext.UserId = null;

        var logEvent = CaptureLogEvent(() => { });

        logEvent.Properties.Should().NotContainKey("UserId");
    }

    private sealed class DelegatingSink(Action<LogEvent> onEmit) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => onEmit(logEvent);
    }
}
