using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Behaviors;
using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Behaviors;

public sealed class AuditBehaviorTests
{
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    [Audited("Created", "Thing")]
    private sealed record CreateThing(string Secret) : ICommand<ThingResponse>;

    private sealed record ThingResponse(Guid Id, string Secret);

    [Audited("Reviewed", "Thing", EntityIdProperty = nameof(ThingId), ActionFromProperty = nameof(Status), DetailProperties = [nameof(Status)])]
    private sealed record ReviewThing(Guid ThingId, string Status, string Notes) : ICommand;

    [Audited("Viewed", "Thing", EntityIdProperty = nameof(ThingId))]
    private sealed record ViewThing(Guid ThingId) : IQuery<ThingResponse>;

    private sealed record NotAudited : ICommand;


    private object?[] SingleCallArguments()
    {
        var call = _audit.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAuditService.RecordAsync));
        return call.GetArguments();
    }

    // Ordem de RecordAsync: action, entityType, entityId, before, after, performedByUserId, tenantId, errorCode, ct.
    private const int Action = 0, EntityType = 1, EntityId = 2, Before = 3, After = 4;

    private AuditBehavior<TRequest, TResponse> Create<TRequest, TResponse>() where TRequest : IRequest<TResponse> =>
        new(_audit, NullLogger<AuditBehavior<TRequest, TResponse>>.Instance);

    [Fact]
    public async Task Creation_UsesTheIdFromTheSuccessValue_AndNeverRecordsFieldValues()
    {
        var id = Guid.NewGuid();
        var behavior = Create<CreateThing, Result<ThingResponse>>();

        await behavior.Handle(new CreateThing("123.456.789-09"), () => Task.FromResult(Result.Success(new ThingResponse(id, "123.456.789-09"))), default);

        var args = SingleCallArguments();
        args[Action].Should().Be(AuditAction.Created);
        args[EntityType].Should().Be("Thing");
        args[EntityId].Should().Be(id.ToString());
        args[Before].Should().BeNull();
        args[After].Should().BeNull("o CPF e demais valores nunca vão para a tabela imutável de auditoria");
    }

    [Fact]
    public async Task ActionFromProperty_MapsTheReviewStatusToTheRightAuditAction_AndRecordsOnlyTheStatus()
    {
        var thingId = Guid.NewGuid();
        var behavior = Create<ReviewThing, Result>();

        await behavior.Handle(new ReviewThing(thingId, "Rejected", "contém dado sigiloso do cliente"), () => Task.FromResult(Result.Success()), default);

        var args = SingleCallArguments();
        args[Action].Should().Be(AuditAction.Rejected);
        args[EntityId].Should().Be(thingId.ToString());
        args[After].Should().BeOfType<Dictionary<string, object?>>()
            .Which.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, object?>("Status", "Rejected"));
        // As Notes (texto livre, possivelmente sigiloso) ficam de fora.
    }

    [Fact]
    public async Task ActionFromProperty_FallsBackToTheDefaultAction_WhenTheValueIsNotAnAuditAction()
    {
        var behavior = Create<ReviewThing, Result>();

        await behavior.Handle(new ReviewThing(Guid.NewGuid(), "Archived", ""), () => Task.FromResult(Result.Success()), default);

        SingleCallArguments()[Action].Should().Be(AuditAction.Reviewed);
    }

    [Fact]
    public async Task Queries_CanBeAudited_AsViewedEvents()
    {
        var thingId = Guid.NewGuid();
        var behavior = Create<ViewThing, Result<ThingResponse>>();

        await behavior.Handle(new ViewThing(thingId), () => Task.FromResult(Result.Success(new ThingResponse(thingId, "x"))), default);

        var args = SingleCallArguments();
        args[Action].Should().Be(AuditAction.Viewed);
        args[EntityId].Should().Be(thingId.ToString());
    }

    [Fact]
    public async Task FailedRequests_AreNotAudited_BecauseNothingHappened()
    {
        var behavior = Create<ReviewThing, Result>();

        var response = await behavior.Handle(
            new ReviewThing(Guid.NewGuid(), "Approved", ""),
            () => Task.FromResult(Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION))), default);

        response.IsFailure.Should().BeTrue();
        await _audit.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default!);
    }

    [Fact]
    public async Task RequestsWithoutTheAttribute_AreIgnored()
    {
        var behavior = Create<NotAudited, Result>();

        await behavior.Handle(new NotAudited(), () => Task.FromResult(Result.Success()), default);

        await _audit.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default!);
    }

    [Fact]
    public async Task WhenRecordingTheAuditFails_TheCompletedOperationIsStillReturned()
    {
        _audit.RecordAsync(default, default!, default!).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("db fora")));
        var behavior = Create<ReviewThing, Result>();

        var response = await behavior.Handle(new ReviewThing(Guid.NewGuid(), "Approved", ""), () => Task.FromResult(Result.Success()), default);

        response.IsSuccess.Should().BeTrue("a operação de negócio já foi confirmada; a falha de auditoria é logada, não propagada");
    }
}
