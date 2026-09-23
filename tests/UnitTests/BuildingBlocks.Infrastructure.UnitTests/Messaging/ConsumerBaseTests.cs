using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.BuildingBlocks.Infrastructure.UnitTests.Outbox;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Messaging;

public sealed class ConsumerBaseTests
{
    private sealed class CountingConsumer(IProcessedEventStore store)
        : ConsumerBase<TestDomainEvent>(store, NullLogger<ConsumerBase<TestDomainEvent>>.Instance)
    {
        public int HandleCallCount { get; private set; }

        protected override Task HandleAsync(TestDomainEvent message, CancellationToken cancellationToken)
        {
            HandleCallCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Consume_WhenEventNotYetProcessed_CallsHandlerAndMarksAsProcessed()
    {
        using var dbContext = TestDbContext.Create();
        var store = new ProcessedEventStore(dbContext);
        var consumer = new CountingConsumer(store);
        var domainEvent = new TestDomainEvent("hello");
        var context = Substitute.For<ConsumeContext<TestDomainEvent>>();
        context.Message.Returns(domainEvent);

        await consumer.Consume(context);

        consumer.HandleCallCount.Should().Be(1);
        (await store.HasBeenProcessedAsync(domainEvent.EventId, nameof(CountingConsumer))).Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenEventAlreadyProcessed_DoesNotCallHandlerAgain()
    {
        using var dbContext = TestDbContext.Create();
        var store = new ProcessedEventStore(dbContext);
        var consumer = new CountingConsumer(store);
        var domainEvent = new TestDomainEvent("hello");
        var context = Substitute.For<ConsumeContext<TestDomainEvent>>();
        context.Message.Returns(domainEvent);

        await consumer.Consume(context);
        await consumer.Consume(context);

        consumer.HandleCallCount.Should().Be(1);
    }
}
