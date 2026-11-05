using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Application.Features.Inbox;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Application.UnitTests.Features;

public class InboxHandlersTests
{
    private readonly InMemoryAtendimentoRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Atendimento Seed(AtendimentoStatus status = AtendimentoStatus.PendingHuman, Guid? assignedTo = null)
    {
        var atendimento = Atendimento.Create(Doubles.TenantId, "5585999998888", null, "Maria", Doubles.Now).Value;

        switch (status)
        {
            case AtendimentoStatus.PendingHuman:
                atendimento.RequestHandoff();
                break;
            case AtendimentoStatus.Human:
                atendimento.AssignToHuman(assignedTo ?? Doubles.LawyerId);
                break;
            case AtendimentoStatus.Closed:
                atendimento.Close();
                break;
        }

        _repository.Add(atendimento);
        return atendimento;
    }

    private SendHumanMessageHandler SendHandler(IBotGateway gateway, Guid? userId = null) =>
        new(_repository, _unitOfWork, gateway, Doubles.Lawyer(userId), Doubles.Clock());

    private ChangeAtendimentoStatusHandler ChangeHandler(Guid? userId = null) =>
        new(_repository, _unitOfWork, Doubles.Lawyer(userId), Doubles.Clock());

    // ---- Advogado envia mensagem (Core -> Bot -> Meta) ----

    [Fact]
    public async Task Send_FromPendingHuman_AssumesTheConversation_ThenDeliversThroughTheBot()
    {
        var atendimento = Seed(AtendimentoStatus.PendingHuman);
        var gateway = Doubles.GatewayReturning(Result.Success<string?>("wamid.OUT1"));

        var result = await SendHandler(gateway).Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Bom dia"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivery.Should().Be("Sent");
        atendimento.Status.Should().Be(AtendimentoStatus.Human);
        atendimento.AssignedUserId.Should().Be(Doubles.LawyerId);

        var mensagem = _repository.Mensagens.Should().ContainSingle().Subject;
        mensagem.Sender.Should().Be(MessageSender.Human);
        mensagem.Direction.Should().Be(MessageDirection.Outbound);
        mensagem.ExternalId.Should().Be("wamid.OUT1");

        await gateway.Received(1).SendTextAsync(
            Doubles.TenantId, atendimento.Id.Value, mensagem.Id.Value, "5585999998888", "Bom dia", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_AuditsBothTheTakeoverAndTheMessage()
    {
        var atendimento = Seed(AtendimentoStatus.PendingHuman);

        await SendHandler(Doubles.GatewayReturning(Result.Success<string?>("w")))
            .Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        _repository.Auditoria.Select(a => a.Action).Should()
            .Equal(AtendimentoAuditAction.Assigned, AtendimentoAuditAction.HumanMessageSent);
        _repository.Auditoria.Should().OnlyContain(a => a.ActorType == AuditActorType.User && a.ActorId == Doubles.LawyerId);
    }

    [Fact]
    public async Task Send_PersistsTheMessageBeforeCallingTheBot_SoItIsNeverLost()
    {
        var atendimento = Seed(AtendimentoStatus.Human);
        var saveCountWhenBotWasCalled = -1;
        var gateway = Substitute.For<IBotGateway>();
        gateway.SendTextAsync(default, default, default, default!, default!, default)
            .ReturnsForAnyArgs(_ =>
            {
                saveCountWhenBotWasCalled = _unitOfWork.SaveCount;
                return Result.Success<string?>("w");
            });

        await SendHandler(gateway).Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        saveCountWhenBotWasCalled.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Send_WhenTheBotFails_KeepsTheMessageMarkedFailed_AndReportsTheError()
    {
        var atendimento = Seed(AtendimentoStatus.Human);
        var gateway = Doubles.GatewayReturning(Result.Failure<string?>(Error.Failure("BOT_UNREACHABLE", "x")));

        var result = await SendHandler(gateway).Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_DELIVERY_FAILED));
        _repository.Mensagens.Should().ContainSingle().Which.Delivery.Should().Be(DeliveryStatus.Failed);
    }

    [Fact]
    public async Task Send_ByAnotherLawyer_IsRefused_AndNothingReachesTheClient()
    {
        var atendimento = Seed(AtendimentoStatus.Human, assignedTo: Guid.NewGuid());
        var gateway = Doubles.GatewayReturning(Result.Success<string?>("w"));

        var result = await SendHandler(gateway).Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED));
        _repository.Mensagens.Should().BeEmpty();
        await gateway.DidNotReceiveWithAnyArgs().SendTextAsync(default, default, default, default!, default!, default);
    }

    [Fact]
    public async Task Send_ToAClosedAtendimento_IsRefused()
    {
        var atendimento = Seed(AtendimentoStatus.Closed);
        var gateway = Doubles.GatewayReturning(Result.Success<string?>("w"));

        var result = await SendHandler(gateway).Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
        await gateway.DidNotReceiveWithAnyArgs().SendTextAsync(default, default, default, default!, default!, default);
    }

    [Fact]
    public async Task Send_FailsForAnUnknownAtendimento()
    {
        var result = await SendHandler(Doubles.GatewayReturning(Result.Success<string?>("w")))
            .Handle(new SendHumanMessageCommand(Guid.NewGuid(), "Oi"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Fact]
    public async Task Send_RequiresAnAuthenticatedUser()
    {
        var atendimento = Seed();
        var caller = Doubles.BotCaller();
        var handler = new SendHumanMessageHandler(
            _repository, _unitOfWork, Doubles.GatewayReturning(Result.Success<string?>("w")), caller, Doubles.Clock());

        var result = await handler.Handle(new SendHumanMessageCommand(atendimento.Id.Value, "Oi"), default);

        result.IsFailure.Should().BeTrue("a chave do Bot não pode falar como advogado");
        _repository.Mensagens.Should().BeEmpty();
    }

    // ---- Assumir / devolver / encerrar ----

    [Fact]
    public async Task Change_ToHuman_AssumesAndAudits()
    {
        var atendimento = Seed(AtendimentoStatus.PendingHuman);

        var result = await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "human"), default);

        result.IsSuccess.Should().BeTrue();
        atendimento.Status.Should().Be(AtendimentoStatus.Human);
        atendimento.AssignedUserId.Should().Be(Doubles.LawyerId);
        _repository.Auditoria.Should().ContainSingle().Which.Should().Match<AtendimentoAuditoria>(a =>
            a.Action == AtendimentoAuditAction.Assigned
            && a.FromStatus == AtendimentoStatus.PendingHuman
            && a.ToStatus == AtendimentoStatus.Human);
    }

    [Fact]
    public async Task Change_ToHuman_Twice_DoesNotAuditTwice()
    {
        var atendimento = Seed(AtendimentoStatus.PendingHuman);

        await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "Human"), default);
        await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "Human"), default);

        _repository.Auditoria.Should().HaveCount(1);
    }

    [Fact]
    public async Task Change_ToHuman_WhenAnotherLawyerHasIt_IsAConflict()
    {
        var atendimento = Seed(AtendimentoStatus.Human, assignedTo: Guid.NewGuid());

        var result = await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "Human"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED));
    }

    [Fact]
    public async Task Change_ToBot_ReturnsTheConversation_AndTheBotMaySpeakAgain()
    {
        var atendimento = Seed(AtendimentoStatus.Human);

        var result = await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "Bot"), default);

        result.IsSuccess.Should().BeTrue();
        atendimento.Status.Should().Be(AtendimentoStatus.Bot);
        atendimento.AssignedUserId.Should().BeNull();
        atendimento.AddMessage(MessageDirection.Outbound, MessageSender.Bot, "Menu", null, Doubles.Now)
            .IsSuccess.Should().BeTrue();
        _repository.Auditoria.Should().ContainSingle().Which.Action.Should().Be(AtendimentoAuditAction.ReturnedToBot);
    }

    [Fact]
    public async Task Change_ToClosed_EndsTheAtendimento()
    {
        var atendimento = Seed(AtendimentoStatus.Human);

        var result = await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(atendimento.Id.Value, "Closed"), default);

        result.IsSuccess.Should().BeTrue();
        atendimento.Status.Should().Be(AtendimentoStatus.Closed);
        _repository.Auditoria.Should().ContainSingle().Which.Action.Should().Be(AtendimentoAuditAction.Closed);
    }

    [Fact]
    public async Task Change_FailsForAnUnknownAtendimento()
    {
        var result = await ChangeHandler().Handle(new ChangeAtendimentoStatusCommand(Guid.NewGuid(), "Closed"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Theory]
    [InlineData("PendingHuman")]
    [InlineData("Whatever")]
    [InlineData("")]
    public void ChangeValidator_OnlyAcceptsHumanBotOrClosed(string status)
    {
        new ChangeAtendimentoStatusValidator()
            .Validate(new ChangeAtendimentoStatusCommand(Guid.NewGuid(), status))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Human")]
    [InlineData("bot")]
    [InlineData("CLOSED")]
    public void ChangeValidator_AcceptsTheThreeManualTargets(string status)
    {
        new ChangeAtendimentoStatusValidator()
            .Validate(new ChangeAtendimentoStatusCommand(Guid.NewGuid(), status))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void SendValidator_RejectsAnEmptyMessage_WithoutEchoingIt()
    {
        var result = new SendHumanMessageValidator().Validate(new SendHumanMessageCommand(Guid.NewGuid(), " "));

        result.IsValid.Should().BeFalse();
    }

    // ---- Vínculo com cliente e leitura ----

    [Fact]
    public async Task LinkCliente_LinksOnce_AndAudits()
    {
        var atendimento = Seed();
        var cliente = Guid.NewGuid();
        var handler = new LinkClienteHandler(_repository, _unitOfWork, Doubles.Lawyer(), Doubles.Clock());

        await handler.Handle(new LinkClienteCommand(atendimento.Id.Value, cliente), default);
        await handler.Handle(new LinkClienteCommand(atendimento.Id.Value, cliente), default);

        atendimento.ClientId.Should().Be(cliente);
        _repository.Auditoria.Should().ContainSingle().Which.Action.Should().Be(AtendimentoAuditAction.ClienteLinked);
    }

    [Fact]
    public async Task ListMensagens_ReturnsChronologicalOrder_AndSignalsOlderPages()
    {
        var atendimento = Seed(AtendimentoStatus.Bot);
        for (var i = 0; i < 5; i++)
        {
            var mensagem = atendimento.AddMessage(
                MessageDirection.Inbound, MessageSender.Client, $"m{i}", $"w{i}", Doubles.Now.AddMinutes(i)).Value;
            _repository.AddMensagem(mensagem);
        }

        var result = await new ListMensagensHandler(_repository)
            .Handle(new ListMensagensQuery(atendimento.Id.Value, null, 3), default);

        result.Value.Items.Select(m => m.Body).Should().Equal("m2", "m3", "m4");
        result.Value.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task ListMensagens_FailsForAnUnknownAtendimento()
    {
        var result = await new ListMensagensHandler(_repository)
            .Handle(new ListMensagensQuery(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Fact]
    public async Task List_PaginatesAndTruncatesThePreview()
    {
        Seed();
        var handler = new ListAtendimentosHandler(_repository);

        var result = await handler.Handle(new ListAtendimentosQuery(Page: 1, PageSize: 20), default);

        result.Value.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(1);
        result.Value.TotalPages.Should().Be(1);
    }
}
