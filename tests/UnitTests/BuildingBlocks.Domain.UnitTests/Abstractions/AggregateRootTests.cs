using Advocacia.BuildingBlocks.Domain.Abstractions;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Abstractions;

public class AggregateRootTests
{
    private sealed record SampleEvent : DomainEvent;

    private sealed class SampleAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void DoSomething() => RaiseDomainEvent(new SampleEvent());
    }

    [Fact]
    public void RaiseDomainEvent_AddsEventToDomainEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.DoSomething();

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SampleEvent>();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllPendingEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.DoSomething();
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }
}
