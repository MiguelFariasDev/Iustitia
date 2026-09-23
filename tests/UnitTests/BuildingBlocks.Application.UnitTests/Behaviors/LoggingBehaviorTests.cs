using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Behaviors;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Advocacia.BuildingBlocks.Application.UnitTests.Behaviors;

public class LoggingBehaviorTests
{
    private sealed record SampleQuery : IQuery<string>;

    private readonly TestLogger<LoggingBehavior<SampleQuery, Result<string>>> _logger = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private LoggingBehavior<SampleQuery, Result<string>> CreateBehavior() => new(_logger, _currentUser);

    [Fact]
    public async Task Handle_OnSuccess_LogsStartAndEndWithDuration()
    {
        var behavior = CreateBehavior();

        var result = await behavior.Handle(new SampleQuery(), () => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("Iniciando"));
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("Concluído"));
    }

    [Fact]
    public async Task Handle_WhenNextThrows_LogsErrorAndRethrows()
    {
        var behavior = CreateBehavior();

        Func<Task> act = () => behavior.Handle(
            new SampleQuery(), () => throw new InvalidOperationException("boom"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Handle_NeverLogsRequestPayload_OnlyRequestName()
    {
        var behavior = CreateBehavior();

        await behavior.Handle(new SampleQuery(), () => Task.FromResult(Result.Success("segredo-nao-deveria-aparecer")), CancellationToken.None);

        _logger.Entries.Should().OnlyContain(e => !e.Message.Contains("segredo-nao-deveria-aparecer"));
    }
}
