using System.Text.Json;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Outbox;

public sealed class OutboxProcessorTests
{
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly IEventTypeResolver _eventTypeResolver = new EventTypeResolver();
    private readonly OutboxProcessorOptions _options = new() { BatchSize = 100, MaxRetries = 3, BackoffBaseSeconds = 1 };

    private OutboxProcessor CreateSut(TestDbContext dbContext) =>
        new(dbContext, _publishEndpoint, _eventTypeResolver, Options.Create(_options), NullLogger<OutboxProcessor>.Instance);

    private static OutboxMessage CreatePendingMessage(TestDomainEvent domainEvent) =>
        new(Guid.NewGuid(), typeof(TestDomainEvent).FullName!, JsonSerializer.Serialize(domainEvent), DateTimeOffset.UtcNow);

    [Fact]
    public async Task ProcessPendingMessagesAsync_WhenPublishSucceeds_MarksMessageAsProcessed()
    {
        using var dbContext = TestDbContext.Create();
        var message = CreatePendingMessage(new TestDomainEvent("hello"));
        dbContext.Set<OutboxMessage>().Add(message);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        var processedCount = await sut.ProcessPendingMessagesAsync();

        processedCount.Should().Be(1);
        message.Status.Should().Be(OutboxStatus.Processed);
        message.ProcessedAt.Should().NotBeNull();
        await _publishEndpoint.Received(1).Publish(Arg.Any<object>(), typeof(TestDomainEvent), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_WhenPublishFails_IncrementsRetryCountAndSchedulesNextRetry()
    {
        using var dbContext = TestDbContext.Create();
        var message = CreatePendingMessage(new TestDomainEvent("hello"));
        dbContext.Set<OutboxMessage>().Add(message);
        await dbContext.SaveChangesAsync();

        _publishEndpoint.Publish(Arg.Any<object>(), typeof(TestDomainEvent), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("transporte indisponível"));

        var sut = CreateSut(dbContext);

        await sut.ProcessPendingMessagesAsync();

        message.Status.Should().Be(OutboxStatus.Pending);
        message.RetryCount.Should().Be(1);
        message.LastError.Should().Contain("transporte indisponível");
        message.NextRetryAt.Should().NotBeNull();
        message.NextRetryAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void MarkAsFailed_WhenRetryCountReachesMaxRetries_MarksMessageAsFailed()
    {
        var message = CreatePendingMessage(new TestDomainEvent("hello"));

        for (var attempt = 0; attempt < _options.MaxRetries - 1; attempt++)
        {
            message.MarkAsFailed("erro transitório", DateTimeOffset.UtcNow, _options.MaxRetries);
            message.Status.Should().Be(OutboxStatus.Pending);
        }

        message.MarkAsFailed("erro final", DateTimeOffset.UtcNow, _options.MaxRetries);

        message.Status.Should().Be(OutboxStatus.Failed);
        message.RetryCount.Should().Be(_options.MaxRetries);
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_RespectsNextRetryAt_DoesNotReprocessBeforeItElapses()
    {
        using var dbContext = TestDbContext.Create();
        var message = CreatePendingMessage(new TestDomainEvent("hello"));
        dbContext.Set<OutboxMessage>().Add(message);
        await dbContext.SaveChangesAsync();

        _publishEndpoint.Publish(Arg.Any<object>(), typeof(TestDomainEvent), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("falha"));

        var sut = CreateSut(dbContext);
        await sut.ProcessPendingMessagesAsync();
        message.RetryCount.Should().Be(1);

        // NextRetryAt está no futuro (backoff de 1s) — uma segunda chamada imediata não deve pegar a mensagem de novo.
        var processedCount = await sut.ProcessPendingMessagesAsync();

        processedCount.Should().Be(0);
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsOnlyPendingMessages()
    {
        using var dbContext = TestDbContext.Create();
        var pending = CreatePendingMessage(new TestDomainEvent("pending"));
        var processed = CreatePendingMessage(new TestDomainEvent("processed"));
        processed.MarkAsProcessed(DateTimeOffset.UtcNow);

        dbContext.Set<OutboxMessage>().AddRange(pending, processed);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        (await sut.GetPendingCountAsync()).Should().Be(1);
    }
}
