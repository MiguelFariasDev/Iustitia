using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Atendimentos.Application.Features.Bot;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Application.UnitTests.Features;

public class BotHandlersTests
{
    private readonly InMemoryAtendimentoRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private StartAtendimentoHandler StartHandler(Guid? tenant = null) =>
        new(_repository, _unitOfWork, Doubles.BotCaller(tenant), Doubles.Clock());

    private AddBotMessageHandler AddMessageHandler() => new(_repository, new InMemoryConsultaRepository(), _unitOfWork, Doubles.Clock());

    private RequestHandoffHandler HandoffHandler() => new(_repository, _unitOfWork, Doubles.Clock());

    private async Task<Guid> StartAsync(string phone = "5585999998888") =>
        (await StartHandler().Handle(new StartAtendimentoCommand(phone), default)).Value.AtendimentoId;

    [Fact]
    public async Task Start_CreatesAnAtendimentoWithTheBot_AndAuditsIt()
    {
        var result = await StartHandler().Handle(new StartAtendimentoCommand("+55 85 99999-8888", null, "Maria"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().BeTrue();
        result.Value.Status.Should().Be("Bot");
        _repository.Atendimentos.Should().ContainSingle().Which.Should().Match<Atendimento>(a =>
            a.TenantId == Doubles.TenantId && a.PhoneNumber == "5585999998888" && a.ContactName == "Maria");
        _repository.Auditoria.Should().ContainSingle().Which.Should().Match<AtendimentoAuditoria>(a =>
            a.Action == AtendimentoAuditAction.Started && a.ActorType == AuditActorType.Bot);
    }

    [Fact]
    public async Task Start_IsIdempotent_ReturningTheOpenAtendimentoOfThePhone()
    {
        var first = await StartHandler().Handle(new StartAtendimentoCommand("5585999998888"), default);
        var second = await StartHandler().Handle(new StartAtendimentoCommand("5585999998888"), default);

        second.Value.AtendimentoId.Should().Be(first.Value.AtendimentoId);
        second.Value.Created.Should().BeFalse();
        _repository.Atendimentos.Should().HaveCount(1);
        _repository.Auditoria.Should().HaveCount(1);
    }

    [Fact]
    public async Task Start_OpensANewAtendimento_WhenThePreviousOneWasClosed()
    {
        var first = await StartAsync();
        _repository.Atendimentos.Single().Close();

        var second = await StartHandler().Handle(new StartAtendimentoCommand("5585999998888"), default);

        second.Value.Created.Should().BeTrue();
        second.Value.AtendimentoId.Should().NotBe(first);
    }

    [Fact]
    public async Task Start_ReportsTheCurrentStatus_SoTheBotNeedsNoSecondCall()
    {
        var id = await StartAsync();
        await HandoffHandler().Handle(new RequestHandoffCommand(id), default);

        var again = await StartHandler().Handle(new StartAtendimentoCommand("5585999998888"), default);

        again.Value.Status.Should().Be("PendingHuman");
    }

    [Fact]
    public async Task Start_RejectsAnInvalidPhone()
    {
        var result = await StartHandler().Handle(new StartAtendimentoCommand("123"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_PHONE_INVALID));
        _repository.Atendimentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_RequiresATenantFromTheApiKey()
    {
        var caller = Substitute.For<Advocacia.BuildingBlocks.Application.Abstractions.ICurrentUser>();
        caller.TenantId.Returns((Guid?)null);
        var handler = new StartAtendimentoHandler(_repository, _unitOfWork, caller, Doubles.Clock());

        var result = await handler.Handle(new StartAtendimentoCommand("5585999998888"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task AddMessage_StoresTheInboundMessage()
    {
        var id = await StartAsync();

        var result = await AddMessageHandler().Handle(
            new AddBotMessageCommand(id, "Inbound", "Client", "Oi", "wamid.1"), default);

        result.IsSuccess.Should().BeTrue();
        _repository.Mensagens.Should().ContainSingle().Which.Should().Match<AtendimentoMensagem>(m =>
            m.Body == "Oi" && m.Sender == MessageSender.Client && m.ExternalId == "wamid.1");
    }

    [Fact]
    public async Task AddMessage_IsIdempotentByExternalId_BecauseMetaRedeliversWebhooks()
    {
        var id = await StartAsync();
        var command = new AddBotMessageCommand(id, "Inbound", "Client", "Oi", "wamid.1");

        var first = await AddMessageHandler().Handle(command, default);
        var second = await AddMessageHandler().Handle(command, default);

        second.Value.MessageId.Should().Be(first.Value.MessageId);
        _repository.Mensagens.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddMessage_TheBotCannotSpeak_WhileTheConversationIsWithAHuman()
    {
        var id = await StartAsync();
        _repository.Atendimentos.Single().AssignToHuman(Doubles.LawyerId);

        var result = await AddMessageHandler().Handle(
            new AddBotMessageCommand(id, "Outbound", "Bot", "Menu", "wamid.2"), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_BOT_BLOCKED));
        _repository.Mensagens.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMessage_FailsForAnUnknownAtendimento()
    {
        var result = await AddMessageHandler().Handle(
            new AddBotMessageCommand(Guid.NewGuid(), "Inbound", "Client", "Oi", null), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Fact]
    public async Task Handoff_MovesToPendingHuman_AndAuditsTheClientRequest()
    {
        var id = await StartAsync();

        var result = await HandoffHandler().Handle(new RequestHandoffCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        _repository.Atendimentos.Single().Status.Should().Be(AtendimentoStatus.PendingHuman);
        _repository.Auditoria.Should().Contain(a =>
            a.Action == AtendimentoAuditAction.HandoffRequested
            && a.ActorType == AuditActorType.Client
            && a.FromStatus == AtendimentoStatus.Bot
            && a.ToStatus == AtendimentoStatus.PendingHuman);
    }

    [Fact]
    public async Task Handoff_RepeatedByTheBot_AuditsOnlyOnce()
    {
        var id = await StartAsync();

        await HandoffHandler().Handle(new RequestHandoffCommand(id), default);
        var again = await HandoffHandler().Handle(new RequestHandoffCommand(id), default);

        again.IsSuccess.Should().BeTrue();
        _repository.Auditoria.Count(a => a.Action == AtendimentoAuditAction.HandoffRequested).Should().Be(1);
    }

    [Fact]
    public async Task Handoff_FailsForAnUnknownAtendimento()
    {
        var result = await HandoffHandler().Handle(new RequestHandoffCommand(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Fact]
    public async Task Status_ReflectsWhoIsInChargeRightNow()
    {
        var id = await StartAsync();
        var handler = new GetAtendimentoStatusHandler(_repository);

        (await handler.Handle(new GetAtendimentoStatusQuery(id), default)).Value.Status.Should().Be("Bot");

        _repository.Atendimentos.Single().AssignToHuman(Doubles.LawyerId);

        (await handler.Handle(new GetAtendimentoStatusQuery(id), default)).Value.Status.Should().Be("Human");
    }

    [Fact]
    public async Task Status_FailsForAnUnknownAtendimento()
    {
        var result = await new GetAtendimentoStatusHandler(_repository)
            .Handle(new GetAtendimentoStatusQuery(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Theory]
    [InlineData("Human")]
    [InlineData("Sideways")]
    public void AddMessageValidator_RejectsASenderTheBotMayNotUse(string sender)
    {
        var validator = new AddBotMessageValidator();

        validator.Validate(new AddBotMessageCommand(Guid.NewGuid(), "Outbound", sender, "x", null))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddMessageValidator_AcceptsAWellFormedBotMessage()
    {
        new AddBotMessageValidator()
            .Validate(new AddBotMessageCommand(Guid.NewGuid(), "outbound", "bot", "x", "wamid.1"))
            .IsValid.Should().BeTrue();
    }
}
