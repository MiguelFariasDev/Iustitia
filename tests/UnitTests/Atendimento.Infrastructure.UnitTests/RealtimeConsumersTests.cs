using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Events;
using Advocacia.Modules.Atendimentos.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Infrastructure.UnitTests;

/// <summary>
/// Os consumers empurram ao navegador SÓ ids e estado. Se um dia alguém acrescentar o texto da
/// mensagem ao payload, estes testes quebram — o canal Realtime nunca pode carregar dado sigiloso.
/// </summary>
public class RealtimeConsumersTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Atendimento = Guid.NewGuid();

    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly IProcessedEventStore _store = Substitute.For<IProcessedEventStore>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly List<(string Channel, string Event, string PayloadJson)> _broadcasts = [];

    public RealtimeConsumersTests()
    {
        _realtime
            .When(r => r.BroadcastAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>()))
            .Do(call => _broadcasts.Add((call.ArgAt<string>(0), call.ArgAt<string>(1), System.Text.Json.JsonSerializer.Serialize(call.ArgAt<object>(2)))));
    }

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task AMessageEvent_GoesToTheTenantChannel_WithOnlyIds()
    {
        var consumer = new AtendimentoMensagemAdicionadaConsumer(
            _realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoMensagemAdicionadaEvent>>.Instance);
        var mensagem = Guid.NewGuid();

        await consumer.Consume(Context(new AtendimentoMensagemAdicionadaEvent(
            Atendimento, Tenant, mensagem, MessageDirection.Inbound, MessageSender.Client)));

        var broadcast = _broadcasts.Should().ContainSingle().Subject;
        broadcast.Channel.Should().Be($"tenant:{Tenant}:atendimentos");
        broadcast.Event.Should().Be("atendimento.message.created");
        broadcast.PayloadJson.Should().Contain(Atendimento.ToString()).And.Contain(mensagem.ToString());
        broadcast.PayloadJson.ToLowerInvariant().Should().NotContain("body").And.NotContain("phone");
    }

    [Fact]
    public async Task AStatusEvent_CarriesTheNewStatusAndTheLawyer()
    {
        var consumer = new AtendimentoStatusAlteradoConsumer(
            _realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoStatusAlteradoEvent>>.Instance);
        var advogada = Guid.NewGuid();

        await consumer.Consume(Context(new AtendimentoStatusAlteradoEvent(
            Atendimento, Tenant, AtendimentoStatus.PendingHuman, AtendimentoStatus.Human, advogada)));

        var broadcast = _broadcasts.Should().ContainSingle().Subject;
        broadcast.Event.Should().Be("atendimento.updated");
        broadcast.PayloadJson.Should().Contain("Human").And.Contain(advogada.ToString());
    }

    [Fact]
    public async Task StartedAndClientLinkedEvents_AlsoRefreshTheInbox()
    {
        await new AtendimentoIniciadoConsumer(_realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoIniciadoEvent>>.Instance)
            .Consume(Context(new AtendimentoIniciadoEvent(Atendimento, Tenant)));
        await new AtendimentoClienteVinculadoConsumer(_realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoClienteVinculadoEvent>>.Instance)
            .Consume(Context(new AtendimentoClienteVinculadoEvent(Atendimento, Tenant, Guid.NewGuid())));

        _broadcasts.Should().HaveCount(2).And.OnlyContain(b =>
            b.Channel == $"tenant:{Tenant}:atendimentos" && b.Event == "atendimento.updated");
    }

    [Fact]
    public async Task ARedeliveredEvent_IsNotBroadcastTwice()
    {
        var evento = new AtendimentoIniciadoEvent(Atendimento, Tenant);
        _store.HasBeenProcessedAsync(evento.EventId, nameof(AtendimentoIniciadoConsumer), Arg.Any<CancellationToken>()).Returns(true);
        var consumer = new AtendimentoIniciadoConsumer(
            _realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoIniciadoEvent>>.Instance);

        await consumer.Consume(Context(evento));

        _broadcasts.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheBroadcastFails_TheErrorPropagates_SoMassTransitRetries()
    {
        _realtime
            .BroadcastAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new HttpRequestException("realtime fora do ar"));
        var consumer = new AtendimentoIniciadoConsumer(
            _realtime, _store, _metrics, NullLogger<ConsumerBase<AtendimentoIniciadoEvent>>.Instance);

        var act = async () => await consumer.Consume(Context(new AtendimentoIniciadoEvent(Atendimento, Tenant)));

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
