using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Events;
using FluentAssertions;

namespace Advocacia.Modules.Atendimentos.Domain.UnitTests;

/// <summary>A máquina de estados do handoff: quem pode falar com o cliente, e quando.</summary>
public class AtendimentoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Advogada = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static Atendimento NewAtendimento(string phone = "5585999998888")
    {
        var result = Atendimento.Create(Tenant, phone, clientId: null, contactName: null, Now);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static Atendimento PendingHuman()
    {
        var atendimento = NewAtendimento();
        atendimento.RequestHandoff().IsSuccess.Should().BeTrue();
        return atendimento;
    }

    private static Atendimento Human()
    {
        var atendimento = PendingHuman();
        atendimento.AssignToHuman(Advogada).IsSuccess.Should().BeTrue();
        return atendimento;
    }

    [Theory]
    [InlineData("+55 (85) 99999-8888", "5585999998888")]
    [InlineData("5585999998888", "5585999998888")]
    [InlineData("  85 99999 8888 ", "85999998888")]
    public void Create_NormalizesThePhoneToDigitsOnly(string raw, string expected)
    {
        NewAtendimento(raw).PhoneNumber.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("1234567890123456")]
    public void Create_RejectsAnInvalidPhone(string? phone)
    {
        var result = Atendimento.Create(Tenant, phone, null, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_PHONE_INVALID));
    }

    [Fact]
    public void Create_StartsWithTheBot_AndRaisesAStartedEvent()
    {
        var atendimento = NewAtendimento();

        atendimento.Status.Should().Be(AtendimentoStatus.Bot);
        atendimento.AssignedUserId.Should().BeNull();
        atendimento.LastMessageAt.Should().Be(Now);
        atendimento.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AtendimentoIniciadoEvent>();
    }

    [Fact]
    public void Create_RejectsAnEmptyTenant()
    {
        Atendimento.Create(Guid.Empty, "5585999998888", null, null, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RequestHandoff_MovesBotToPendingHuman()
    {
        var atendimento = NewAtendimento();

        atendimento.RequestHandoff().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.PendingHuman);
        atendimento.DomainEvents.OfType<AtendimentoStatusAlteradoEvent>().Should().ContainSingle()
            .Which.Should().Match<AtendimentoStatusAlteradoEvent>(e =>
                e.From == AtendimentoStatus.Bot && e.To == AtendimentoStatus.PendingHuman);
    }

    [Fact]
    public void RequestHandoff_IsIdempotent_AndRaisesNoSecondEvent()
    {
        var atendimento = PendingHuman();
        var eventsBefore = atendimento.DomainEvents.Count;

        atendimento.RequestHandoff().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.PendingHuman);
        atendimento.DomainEvents.Should().HaveCount(eventsBefore);
    }

    [Fact]
    public void RequestHandoff_DoesNotStealAConversationAlreadyWithAHuman()
    {
        var atendimento = Human();

        atendimento.RequestHandoff().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Human);
        atendimento.AssignedUserId.Should().Be(Advogada);
    }

    [Fact]
    public void RequestHandoff_FailsWhenClosed()
    {
        var atendimento = NewAtendimento();
        atendimento.Close();

        atendimento.RequestHandoff().Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
    }

    [Fact]
    public void AssignToHuman_FromPendingHuman_SetsTheLawyer()
    {
        var atendimento = PendingHuman();

        atendimento.AssignToHuman(Advogada).IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Human);
        atendimento.AssignedUserId.Should().Be(Advogada);
    }

    [Fact]
    public void AssignToHuman_FromBot_IsAllowed_WhenTheLawyerAnswersWithoutWaitingForARequest()
    {
        var atendimento = NewAtendimento();

        atendimento.AssignToHuman(Advogada).IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Human);
    }

    [Fact]
    public void AssignToHuman_ByTheSameLawyer_IsIdempotent()
    {
        var atendimento = Human();

        atendimento.AssignToHuman(Advogada).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignToHuman_ByAnotherLawyer_IsAConflict()
    {
        var atendimento = Human();

        var result = atendimento.AssignToHuman(Guid.NewGuid());

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED));
        atendimento.AssignedUserId.Should().Be(Advogada);
    }

    [Fact]
    public void AssignToHuman_FailsWhenClosed()
    {
        var atendimento = NewAtendimento();
        atendimento.Close();

        atendimento.AssignToHuman(Advogada).Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
    }

    [Fact]
    public void ReturnToBot_FromHuman_ClearsTheLawyer()
    {
        var atendimento = Human();

        atendimento.ReturnToBot().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Bot);
        atendimento.AssignedUserId.Should().BeNull();
    }

    [Fact]
    public void ReturnToBot_FromPendingHuman_CancelsTheQueue()
    {
        var atendimento = PendingHuman();

        atendimento.ReturnToBot().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Bot);
    }

    [Fact]
    public void ReturnToBot_WhenAlreadyWithTheBot_IsAnInvalidTransition()
    {
        NewAtendimento().ReturnToBot().Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_INVALID_STATUS_TRANSITION));
    }

    [Theory]
    [InlineData(AtendimentoStatus.Bot)]
    [InlineData(AtendimentoStatus.PendingHuman)]
    [InlineData(AtendimentoStatus.Human)]
    public void Close_IsAllowedFromAnyOpenStatus(AtendimentoStatus from)
    {
        var atendimento = from switch
        {
            AtendimentoStatus.PendingHuman => PendingHuman(),
            AtendimentoStatus.Human => Human(),
            _ => NewAtendimento(),
        };

        atendimento.Close().IsSuccess.Should().BeTrue();

        atendimento.Status.Should().Be(AtendimentoStatus.Closed);
        atendimento.AssignedUserId.Should().BeNull();
        atendimento.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Close_IsTerminal()
    {
        var atendimento = NewAtendimento();
        atendimento.Close();

        atendimento.Close().Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
        atendimento.ReturnToBot().Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
    }

    // ---- AddMessage: a regra inegociável é "o bot NUNCA fala com uma conversa em Human" ----

    [Fact]
    public void AddMessage_BotCannotSpeakWhileTheConversationIsWithAHuman()
    {
        var atendimento = Human();

        var result = atendimento.AddMessage(MessageDirection.Outbound, MessageSender.Bot, "Olá!", null, Now);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_BOT_BLOCKED));
    }

    [Fact]
    public void AddMessage_BotMayStillSendTheHandoffNotice_WhilePendingHuman()
    {
        var atendimento = PendingHuman();

        var result = atendimento.AddMessage(
            MessageDirection.Outbound, MessageSender.Bot, "Aguarde, um advogado vai te atender.", "wamid.1", Now);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AddMessage_HumanCanOnlySpeakAfterAssuming()
    {
        var atendimento = PendingHuman();

        atendimento.AddMessage(MessageDirection.Outbound, MessageSender.Human, "Olá", null, Now)
            .Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER));
    }

    [Fact]
    public void AddMessage_HumanMessageStartsPending_BecauseDeliveryHappensAfterwards()
    {
        var atendimento = Human();

        var mensagem = atendimento.AddMessage(MessageDirection.Outbound, MessageSender.Human, " Bom dia ", null, Now).Value;

        mensagem.Delivery.Should().Be(DeliveryStatus.Pending);
        mensagem.Body.Should().Be("Bom dia");
        mensagem.TenantId.Should().Be(Tenant);
    }

    [Fact]
    public void AddMessage_InboundFromTheClient_IsAllowedInAnyOpenStatus_AndUpdatesLastMessageAt()
    {
        var atendimento = Human();
        var later = Now.AddMinutes(5);

        var result = atendimento.AddMessage(MessageDirection.Inbound, MessageSender.Client, "Doutora?", "wamid.2", later);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivery.Should().Be(DeliveryStatus.Sent);
        atendimento.LastMessageAt.Should().Be(later);
    }

    [Theory]
    [InlineData(MessageDirection.Inbound, MessageSender.Bot)]
    [InlineData(MessageDirection.Inbound, MessageSender.Human)]
    [InlineData(MessageDirection.Outbound, MessageSender.Client)]
    public void AddMessage_RejectsAnInconsistentDirectionAndSender(MessageDirection direction, MessageSender sender)
    {
        NewAtendimento().AddMessage(direction, sender, "x", null, Now)
            .Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMessage_RejectsAnEmptyBody(string? body)
    {
        NewAtendimento().AddMessage(MessageDirection.Inbound, MessageSender.Client, body, null, Now)
            .Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_MESSAGE_EMPTY));
    }

    [Fact]
    public void AddMessage_RejectsABodyOverTheWhatsAppLimit()
    {
        var body = new string('a', Atendimento.MaxBodyLength + 1);

        NewAtendimento().AddMessage(MessageDirection.Inbound, MessageSender.Client, body, null, Now)
            .Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_MESSAGE_TOO_LONG));
    }

    [Fact]
    public void AddMessage_FailsWhenClosed()
    {
        var atendimento = NewAtendimento();
        atendimento.Close();

        atendimento.AddMessage(MessageDirection.Inbound, MessageSender.Client, "oi", null, Now)
            .Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CLOSED));
    }

    [Fact]
    public void AddMessage_RaisesAnEventWithoutPersonalData()
    {
        var atendimento = NewAtendimento();

        atendimento.AddMessage(MessageDirection.Inbound, MessageSender.Client, "meu CPF é 123", "wamid.3", Now);

        var evento = atendimento.DomainEvents.OfType<AtendimentoMensagemAdicionadaEvent>().Should().ContainSingle().Subject;
        evento.GetType().GetProperties().Select(p => p.Name).Should()
            .NotContain(["Body", "PhoneNumber", "Phone", "Texto"], "eventos atravessam a outbox e não carregam dado pessoal");
    }

    [Fact]
    public void LinkCliente_IsIdempotent_AndRaisesTheEventOnlyOnce()
    {
        var atendimento = NewAtendimento();
        var cliente = Guid.NewGuid();

        atendimento.LinkCliente(cliente);
        atendimento.LinkCliente(cliente);

        atendimento.ClientId.Should().Be(cliente);
        atendimento.DomainEvents.OfType<AtendimentoClienteVinculadoEvent>().Should().ContainSingle();
    }

    [Fact]
    public void UpdateContactName_KeepsTheKnownNameWhenTheNewOneIsBlank()
    {
        var atendimento = Atendimento.Create(Tenant, "5585999998888", null, "Maria", Now).Value;

        atendimento.UpdateContactName("  ");

        atendimento.ContactName.Should().Be("Maria");
    }
}
