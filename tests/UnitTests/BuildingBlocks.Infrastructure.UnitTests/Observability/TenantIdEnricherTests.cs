using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using FluentAssertions;
using Serilog;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class TenantIdEnricherTests
{
    private static LogEvent CaptureLogEvent(Action beforeLog)
    {
        LogEvent? captured = null;
        using var logger = new LoggerConfiguration()
            .Enrich.With<TenantIdEnricher>()
            .WriteTo.Sink(new DelegatingSink(logEvent => captured = logEvent))
            .CreateLogger();

        beforeLog();
        logger.Information("mensagem de teste");

        return captured!;
    }

    [Fact]
    public void Enrich_WhenTenantIsSet_AddsProperty()
    {
        var tenantContext = new AsyncLocalTenantContext();
        var tenantId = Guid.NewGuid();

        var logEvent = CaptureLogEvent(() => tenantContext.SetTenant(tenantId));

        logEvent.Properties.Should().ContainKey("TenantId");
    }

    private sealed class DelegatingSink(Action<LogEvent> onEmit) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => onEmit(logEvent);
    }
}
