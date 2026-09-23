using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Behaviors;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.BuildingBlocks.Application.UnitTests.Behaviors;

public class TransactionBehaviorTests
{
    private sealed record SampleCommand : ICommand<string>;

    private sealed record SampleQuery : IQuery<string>;

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_CommandThatSucceeds_CommitsTransaction()
    {
        var behavior = new TransactionBehavior<SampleCommand, Result<string>>(_unitOfWork);

        var result = await behavior.Handle(new SampleCommand(), () => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandThatFails_RollsBackTransaction()
    {
        var behavior = new TransactionBehavior<SampleCommand, Result<string>>(_unitOfWork);
        var error = Error.Validation("x", "y");

        var result = await behavior.Handle(new SampleCommand(), () => Task.FromResult(Result.Failure<string>(error)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandWhoseHandlerThrows_RollsBackAndRethrows()
    {
        var behavior = new TransactionBehavior<SampleCommand, Result<string>>(_unitOfWork);

        Func<Task> act = () => behavior.Handle(
            new SampleCommand(), () => throw new InvalidOperationException("boom"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Query_NeverOpensTransaction()
    {
        var behavior = new TransactionBehavior<SampleQuery, Result<string>>(_unitOfWork);

        var result = await behavior.Handle(new SampleQuery(), () => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceiveWithAnyArgs().BeginTransactionAsync(cancellationToken: default);
    }
}
