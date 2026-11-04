using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class CustomMetricsTests
{
    [Fact]
    public void RecordOutboxProcessed_EmitsCounterMeasurement()
    {
        var sut = new CustomMetrics();
        var measurements = new List<long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MeterNames.Advocacia && instrument.Name == "outbox.processed.count")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();

        sut.RecordOutboxProcessed(3);

        measurements.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public void RecordOutboxPendingCount_UpdatesObservableGauge()
    {
        var sut = new CustomMetrics();
        var measurements = new List<int>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MeterNames.Advocacia && instrument.Name == "outbox.pending.count")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();

        sut.RecordOutboxPendingCount(42);
        listener.RecordObservableInstruments();

        measurements.Should().Contain(42);
    }

    [Fact]
    public void RecordHandlerExecuted_DoesNotThrow()
    {
        var sut = new CustomMetrics();

        var act = () => sut.RecordHandlerExecuted("CreateTenantCommand", TimeSpan.FromMilliseconds(120), success: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordConsumerProcessed_DoesNotThrow()
    {
        var sut = new CustomMetrics();

        var act = () => sut.RecordConsumerProcessed("UserInvitedConsumer", success: false);

        act.Should().NotThrow();
    }
}
