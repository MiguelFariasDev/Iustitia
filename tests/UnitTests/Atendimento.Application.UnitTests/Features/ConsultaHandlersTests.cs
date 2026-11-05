using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Features.Bot;
using Advocacia.Modules.Atendimentos.Application.Features.Consulta;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Consulta;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Application.UnitTests.Features;

/// <summary>
/// A consulta de andamento pelo bot: verificação de identidade no Core, isolamento entre clientes e
/// escritórios, e o documento que não pode ir parar em lugar nenhum.
/// </summary>
public class ConsultaHandlersTests
{
    private const string Phone = "5585999998888";
    private const string Cpf = "12345678909";

    private readonly Guid _client = Guid.NewGuid();
    private readonly InMemoryAtendimentoRepository _atendimentos = new();
    private readonly InMemoryConsultaRepository _sessoes = new();
    private readonly FakeIdentityDirectory _identity = new();
    private readonly FakeProcessReader _processes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MutableClock _clock = new(Doubles.Now);

    private sealed class MutableClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;

        public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    public ConsultaHandlersTests()
    {
        _identity.ClientsByPhone[(Doubles.TenantId, Phone)] = _client;
        _identity.Documents[(Doubles.TenantId, _client)] = Cpf;
    }

    private Atendimento Seed(AtendimentoStatus status = AtendimentoStatus.Bot, string phone = Phone)
    {
        var atendimento = Atendimento.Create(Doubles.TenantId, phone, null, "Maria", Doubles.Now).Value;
        switch (status)
        {
            case AtendimentoStatus.PendingHuman: atendimento.RequestHandoff(); break;
            case AtendimentoStatus.Human: atendimento.AssignToHuman(Doubles.LawyerId); break;
            case AtendimentoStatus.Closed: atendimento.Close(); break;
        }

        _atendimentos.Add(atendimento);
        return atendimento;
    }

    private static ClientProcessSummary Process(string title, string phase = "Em andamento") =>
        new(Guid.NewGuid(), title, phase, "Sentença publicada em 10/10/2026.", Doubles.Now);

    private IniciarConsultaHandler Iniciar(Guid? tenant = null) =>
        new(_atendimentos, _sessoes, _identity, _unitOfWork, Doubles.BotCaller(tenant), _clock);

    private VerificarConsultaHandler Verificar(Guid? tenant = null) =>
        new(_atendimentos, _sessoes, _identity, _processes, _unitOfWork, Doubles.BotCaller(tenant), _clock);

    private GetConsultaProcessoHandler VerProcesso(Guid? tenant = null) =>
        new(_atendimentos, _sessoes, _processes, _unitOfWork, Doubles.BotCaller(tenant), _clock);

    private async Task<string> IniciarAsync(Atendimento a) =>
        (await Iniciar().Handle(new IniciarConsultaCommand(a.Id.Value), default)).Value.Etapa;

    private async Task<VerificarConsultaResponse> VerificarAsync(Atendimento a, string documento) =>
        (await Verificar().Handle(new VerificarConsultaCommand(a.Id.Value, documento), default)).Value;

    // ---- só com o bot conduzindo ----

    [Theory]
    [InlineData(AtendimentoStatus.PendingHuman)]
    [InlineData(AtendimentoStatus.Human)]
    [InlineData(AtendimentoStatus.Closed)]
    public async Task EveryCall_IsRefusedWith409_UnlessTheBotIsInCharge(AtendimentoStatus status)
    {
        var atendimento = Seed(status);
        var id = atendimento.Id.Value;

        var iniciar = await Iniciar().Handle(new IniciarConsultaCommand(id), default);
        var verificar = await Verificar().Handle(new VerificarConsultaCommand(id, Cpf), default);
        var processo = await VerProcesso().Handle(new GetConsultaProcessoQuery(id, 1), default);

        foreach (var error in new[] { iniciar.Error, verificar.Error, processo.Error })
        {
            error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CONSULTA_REQUER_BOT));
            error.Type.Should().Be(ErrorType.Conflict);
        }

        _sessoes.Sessoes.Should().BeEmpty("nada é criado fora do status Bot");
        _identity.Comparisons.Should().BeEmpty("nem o documento é comparado");
    }

    [Fact]
    public async Task AnUnknownAtendimento_Is404()
    {
        var result = await Iniciar().Handle(new IniciarConsultaCommand(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_NOT_FOUND));
    }

    [Fact]
    public async Task WhenTheLawyerTakesOverMidFlow_TheNextCallIsRefused()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        atendimento.AssignToHuman(Doubles.LawyerId);
        var result = await Verificar().Handle(new VerificarConsultaCommand(atendimento.Id.Value, Cpf), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CONSULTA_REQUER_BOT));
        _identity.Comparisons.Should().BeEmpty();
    }

    // ---- iniciar ----

    [Fact]
    public async Task Start_ForAnOwnerOfTheNumber_WaitsForTheDocument()
    {
        var atendimento = Seed();

        (await IniciarAsync(atendimento)).Should().Be("AguardandoDocumento");

        _sessoes.Sessoes.Should().ContainSingle().Which.Should().Match<ConsultaSessao>(s =>
            s.ClientId == _client && s.TenantId == Doubles.TenantId && s.AtendimentoId == atendimento.Id);
    }

    [Fact]
    public async Task Start_ForAPhoneWithoutRegistration_IsDenied_WithNoSession()
    {
        var atendimento = Seed(phone: "5511911112222");

        (await IniciarAsync(atendimento)).Should().Be("Negado");

        _sessoes.Sessoes.Should().BeEmpty();
    }

    [Fact]
    public async Task TheDeniedAnswer_ForAnUnknownPhone_IsIdenticalToThatOfAWrongDocument()
    {
        // Quem pergunta não pode descobrir, pela resposta, se o número pertence a um cliente.
        var stranger = Seed(phone: "5511911112222");
        var strangerStart = await Iniciar().Handle(new IniciarConsultaCommand(stranger.Id.Value), default);

        var known = Seed(phone: "5585911110000");
        _identity.ClientsByPhone[(Doubles.TenantId, "5585911110000")] = Guid.NewGuid();
        await IniciarAsync(known);
        var wrongDocument = await Verificar().Handle(new VerificarConsultaCommand(known.Id.Value, "00000000000"), default);

        strangerStart.Value.Etapa.Should().Be("Negado");
        wrongDocument.Value.Etapa.Should().Be("Negado");
        wrongDocument.Value.Processos.Should().BeNull();
    }

    [Fact]
    public async Task Start_ForAPhoneOfTheOtherTenant_IsDenied()
    {
        var atendimento = Seed();
        var otherTenant = Guid.NewGuid();

        var result = await Iniciar(otherTenant).Handle(new IniciarConsultaCommand(atendimento.Id.Value), default);

        result.Value.Etapa.Should().Be("Negado", "o telefone só foi procurado no escritório do atendimento");
    }

    [Fact]
    public async Task Starting_AgainWhileAwaiting_RestartsWithAFreshSession_AndKeepsTheFailures()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, "11111111111");
        await VerificarAsync(atendimento, "22222222222");

        await IniciarAsync(atendimento);

        _sessoes.Sessoes.Should().HaveCount(2);
        _sessoes.Sessoes[0].Etapa(_clock.UtcNow).Should().Be(ConsultaEtapa.Expirada, "só vale a sessão mais nova");
        _sessoes.Sessoes[1].FailedAttempts.Should().Be(2, "recomeçar não zera as falhas");
    }

    // ---- verificar ----

    [Fact]
    public async Task TheRightDocument_Verifies_AndListsOnlyTitlesWithIndexesFromOne()
    {
        var atendimento = Seed();
        _processes.Store.Add((Doubles.TenantId, _client, Process("Ação de cobrança · 0001234-56.2024.8.06.0001")));
        _processes.Store.Add((Doubles.TenantId, _client, Process("Divórcio · 0007777-12.2023.8.06.0002")));
        await IniciarAsync(atendimento);

        var response = await VerificarAsync(atendimento, Cpf);

        response.Etapa.Should().Be("Verificado");
        response.Processos.Should().Equal(
            new ConsultaProcessoItem(1, "Ação de cobrança · 0001234-56.2024.8.06.0001"),
            new ConsultaProcessoItem(2, "Divórcio · 0007777-12.2023.8.06.0002"));
    }

    [Theory]
    [InlineData("123.456.789-09")]
    [InlineData("123 456 789 09")]
    [InlineData("  12345678909 ")]
    [InlineData("CPF 123.456.789-09")]
    public async Task ThePunctuationOfTheDocument_DoesNotMatter(string typed)
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        (await VerificarAsync(atendimento, typed)).Etapa.Should().Be("Verificado");

        _identity.Comparisons.Single().Digits.Should().Be(Cpf);
    }

    [Fact]
    public async Task AVerifiedClientWithNoProcesses_GetsAnEmptyList_NotAnError()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        var response = await VerificarAsync(atendimento, Cpf);

        response.Etapa.Should().Be("Verificado");
        response.Processos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("123456789012")]
    public async Task ADocumentOfTheWrongSize_IsDenied_AndCountsAsAFailure_WithoutTouchingTheDirectory(string typed)
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        (await VerificarAsync(atendimento, typed)).Etapa.Should().Be("Negado");

        _sessoes.Sessoes.Single().FailedAttempts.Should().Be(1);
        _identity.Comparisons.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyingWithoutStarting_ReportsExpired()
    {
        var atendimento = Seed();

        (await VerificarAsync(atendimento, Cpf)).Etapa.Should().Be("Expirada");

        _identity.Comparisons.Should().BeEmpty();
    }

    [Fact]
    public async Task AfterTenMinutes_TheSessionIsExpired_AndTheRightDocumentIsNotAccepted()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        _clock.UtcNow = Doubles.Now.AddMinutes(10);
        var response = await VerificarAsync(atendimento, Cpf);

        response.Etapa.Should().Be("Expirada");
        _identity.Comparisons.Should().BeEmpty();
    }

    [Fact]
    public async Task ThreeFailures_Block_AndTheRightDocumentIsThenRefused()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        (await VerificarAsync(atendimento, "11111111111")).Etapa.Should().Be("Negado");
        (await VerificarAsync(atendimento, "22222222222")).Etapa.Should().Be("Negado");
        (await VerificarAsync(atendimento, "33333333333")).Etapa.Should().Be("Bloqueado");

        (await VerificarAsync(atendimento, Cpf)).Etapa.Should().Be("Bloqueado");
        _identity.Comparisons.Should().HaveCount(3, "bloqueado, o documento nem chega a ser comparado");
    }

    [Fact]
    public async Task TheBlock_BelongsToTheClient_NotToTheConversation()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        for (var i = 0; i < 3; i++)
        {
            await VerificarAsync(atendimento, "11111111111");
        }

        // Outra conversa do mesmo cliente (a anterior foi encerrada): continua bloqueado.
        atendimento.Close();
        var another = Seed();

        (await IniciarAsync(another)).Should().Be("Bloqueado");
        _sessoes.Sessoes.Should().HaveCount(1, "nenhuma sessão nova durante o bloqueio");
    }

    [Fact]
    public async Task RestartingLoopDoesNotResetTheCounter_TheClientIsStillBlockedAfterThreeFailures()
    {
        var atendimento = Seed();
        var etapas = new List<string>();

        for (var round = 0; round < 3; round++)
        {
            await IniciarAsync(atendimento);
            etapas.Add((await VerificarAsync(atendimento, "11111111111")).Etapa);
            etapas.Add((await VerificarAsync(atendimento, "22222222222")).Etapa);
        }

        etapas.Should().Contain("Bloqueado", "iniciar em laço, dois palpites por vez, não contorna as 3 falhas");
        _identity.Comparisons.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task AfterTheBlockExpires_TheClientHasAFreshStart()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        for (var i = 0; i < 3; i++)
        {
            await VerificarAsync(atendimento, "11111111111");
        }

        _clock.UtcNow = Doubles.Now.AddMinutes(31);

        (await IniciarAsync(atendimento)).Should().Be("AguardandoDocumento");
        _sessoes.Sessoes.Last().FailedAttempts.Should().Be(0);
        (await VerificarAsync(atendimento, Cpf)).Etapa.Should().Be("Verificado");
    }

    [Fact]
    public async Task VerifyingAnAlreadyVerifiedSession_ReturnsTheListWithoutComparingAnything()
    {
        var atendimento = Seed();
        _processes.Store.Add((Doubles.TenantId, _client, Process("Ação A")));
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);
        _identity.Comparisons.Clear();

        var again = await VerificarAsync(atendimento, "99999999999");

        again.Etapa.Should().Be("Verificado");
        again.Processos.Should().ContainSingle().Which.Titulo.Should().Be("Ação A");
        _identity.Comparisons.Should().BeEmpty();
    }

    // ---- processos: isolamento ----

    [Fact]
    public async Task AVerifiedClient_SeesOnlyTheirOwnProcesses_NotAnotherClientsNorAnotherTenants()
    {
        var atendimento = Seed();
        var otherClient = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var mine = Process("Meu processo");
        var theirs = Process("Processo de outro cliente");
        var foreign = Process("Processo de outro escritório");
        _processes.Store.Add((Doubles.TenantId, _client, mine));
        _processes.Store.Add((Doubles.TenantId, otherClient, theirs));
        _processes.Store.Add((otherTenant, _client, foreign));
        await IniciarAsync(atendimento);

        var response = await VerificarAsync(atendimento, Cpf);

        response.Processos.Should().ContainSingle().Which.Titulo.Should().Be("Meu processo");
        _processes.Calls.Should().OnlyContain(call => call.Contains(Doubles.TenantId.ToString()) && call.Contains(_client.ToString()));
    }

    [Fact]
    public async Task TheProcessDetail_IsReadFromTheIdsFixedAtVerification_ForTheSessionsClientAndTenant()
    {
        var atendimento = Seed();
        var mine = Process("Meu processo", "Em andamento");
        _processes.Store.Add((Doubles.TenantId, _client, mine));
        _processes.Store.Add((Doubles.TenantId, Guid.NewGuid(), Process("Alheio")));
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);

        var result = await VerProcesso().Handle(new GetConsultaProcessoQuery(atendimento.Id.Value, 1), default);

        result.Value.Should().Be(new ConsultaProcessoResponse("Meu processo", "Em andamento", mine.LastMovement, mine.UpdatedAt));
        _processes.Calls.Last().Should().Be($"get:{Doubles.TenantId}:{_client}:{mine.ProcessId}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task AnIndexOutsideTheList_Is404_AndNeverReachesTheReader(int indice)
    {
        var atendimento = Seed();
        _processes.Store.Add((Doubles.TenantId, _client, Process("Único")));
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);
        _processes.Calls.Clear();

        var result = await VerProcesso().Handle(new GetConsultaProcessoQuery(atendimento.Id.Value, indice), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CONSULTA_PROCESSO_NOT_FOUND));
        _processes.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task AProcessThatLeftTheClientsAccountAfterVerification_Is404()
    {
        var atendimento = Seed();
        var process = Process("Será removido");
        _processes.Store.Add((Doubles.TenantId, _client, process));
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);
        _processes.Store.Clear();

        var result = await VerProcesso().Handle(new GetConsultaProcessoQuery(atendimento.Id.Value, 1), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CONSULTA_PROCESSO_NOT_FOUND));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("waiting")]
    [InlineData("denied")]
    [InlineData("blocked")]
    [InlineData("expired")]
    public async Task TheDetail_IsForbidden_UnlessTheSessionIsVerifiedAndAlive(string situation)
    {
        var atendimento = Seed();
        _processes.Store.Add((Doubles.TenantId, _client, Process("Segredo")));

        if (situation != "none")
        {
            await IniciarAsync(atendimento);
        }

        if (situation == "denied")
        {
            await VerificarAsync(atendimento, "11111111111");
        }

        if (situation == "blocked")
        {
            for (var i = 0; i < 3; i++)
            {
                await VerificarAsync(atendimento, "11111111111");
            }
        }

        if (situation == "expired")
        {
            await VerificarAsync(atendimento, Cpf);
            _clock.UtcNow = Doubles.Now.AddMinutes(11);
        }

        var result = await VerProcesso().Handle(new GetConsultaProcessoQuery(atendimento.Id.Value, 1), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_CONSULTA_NAO_VERIFICADA));
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    // ---- auditoria ----

    [Fact]
    public async Task EveryStep_IsAudited_WithTheClientAsActor_AndNeverTheDocument()
    {
        var atendimento = Seed();
        var process = Process("Meu processo");
        _processes.Store.Add((Doubles.TenantId, _client, process));

        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, "99999999999");
        await VerificarAsync(atendimento, Cpf);
        await VerProcesso().Handle(new GetConsultaProcessoQuery(atendimento.Id.Value, 1), default);

        _atendimentos.Auditoria.Select(a => a.Action).Should().Equal(
            AtendimentoAuditAction.ConsultaIniciada,
            AtendimentoAuditAction.ConsultaNegada,
            AtendimentoAuditAction.ConsultaVerificada,
            AtendimentoAuditAction.ConsultaProcessoVisualizado);
        _atendimentos.Auditoria.Should().OnlyContain(a => a.ActorType == AuditActorType.Client && a.ActorId == _client);
        _atendimentos.Auditoria.Last().ReferenceId.Should().Be(process.ProcessId, "registra QUAL processo foi visto");

        var everything = string.Join('|', _atendimentos.Auditoria.Select(a =>
            $"{a.Action}{a.ActorId}{a.ReferenceId}{a.FromStatus}{a.ToStatus}{a.OccurredAt:O}"));
        everything.Should().NotContain(Cpf).And.NotContain("99999999999");
    }

    [Fact]
    public async Task ADeniedStart_ForAnUnknownPhone_IsAuditedWithoutAnActor()
    {
        var atendimento = Seed(phone: "5511911112222");

        await IniciarAsync(atendimento);

        _atendimentos.Auditoria.Should().ContainSingle().Which.Should().Match<AtendimentoAuditoria>(a =>
            a.Action == AtendimentoAuditAction.ConsultaNegada && a.ActorId == null);
    }

    [Fact]
    public async Task ABlockedAttempt_IsAuditedToo()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        for (var i = 0; i < 3; i++)
        {
            await VerificarAsync(atendimento, "11111111111");
        }

        await VerificarAsync(atendimento, "11111111111");

        _atendimentos.Auditoria.Count(a => a.Action == AtendimentoAuditAction.ConsultaBloqueada).Should().Be(2);
    }

    // ---- o documento não vaza ----

    [Fact]
    public void TheCommandsToString_NeverShowsTheDocument()
    {
        var command = new VerificarConsultaCommand(Guid.NewGuid(), "123.456.789-09");

        command.ToString().Should().NotContain("123").And.NotContain("789").And.Contain("***");
    }

    [Fact]
    public async Task ADeniedResponse_NeverEchoesTheDocument()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        var result = await Verificar().Handle(new VerificarConsultaCommand(atendimento.Id.Value, "98765432100"), default);

        System.Text.Json.JsonSerializer.Serialize(result.Value).Should().NotContain("98765432100");
    }

    [Fact]
    public void TheValidatorMessage_NeverEchoesTheDocument()
    {
        var long30 = new string('9', 31);

        var result = new VerificarConsultaValidator().Validate(new VerificarConsultaCommand(Guid.NewGuid(), long30));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().NotContain(m => m.Contains("999"));
        new VerificarConsultaValidator().Validate(new VerificarConsultaCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
    }

    // ---- redação no histórico ----

    private async Task<string> RecordInboundAsync(Atendimento atendimento, string body, string externalId)
    {
        var handler = new AddBotMessageHandler(_atendimentos, _sessoes, _unitOfWork, _clock);
        await handler.Handle(new AddBotMessageCommand(atendimento.Id.Value, "Inbound", "Client", body, externalId), default);
        return _atendimentos.Mensagens.Single(m => m.ExternalId == externalId).Body;
    }

    [Fact]
    public async Task WhileTheDocumentIsBeingAsked_TheClientsMessageIsStoredWithoutItsText()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);

        var stored = await RecordInboundAsync(atendimento, "123.456.789-09", "wamid.doc1");

        stored.Should().Be(AddBotMessageHandler.RedactedInboundBody).And.NotContain("789");
    }

    [Fact]
    public async Task EvenWhenTheBotRecordsAfterVerifying_TheDocumentIsNotStored()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);

        var stored = await RecordInboundAsync(atendimento, "123.456.789-09", "wamid.doc2");

        stored.Should().Be(AddBotMessageHandler.RedactedInboundBody);
    }

    [Fact]
    public async Task OrdinaryMessages_AreStoredNormally_WithoutAnyVerification()
    {
        var atendimento = Seed();

        (await RecordInboundAsync(atendimento, "Bom dia", "wamid.a")).Should().Be("Bom dia");
    }

    [Fact]
    public async Task OnceTheVerificationWindowPasses_MessagesAreStoredNormallyAgain()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        await VerificarAsync(atendimento, Cpf);

        _clock.UtcNow = Doubles.Now.AddMinutes(11);

        (await RecordInboundAsync(atendimento, "Obrigada!", "wamid.b")).Should().Be("Obrigada!");
    }

    [Fact]
    public async Task OnlyTheClientsMessagesAreRedacted_TheBotsOwnRepliesAreNot()
    {
        var atendimento = Seed();
        await IniciarAsync(atendimento);
        var handler = new AddBotMessageHandler(_atendimentos, _sessoes, _unitOfWork, _clock);

        await handler.Handle(new AddBotMessageCommand(atendimento.Id.Value, "Outbound", "Bot", "Digite seu CPF", "bot:1"), default);

        _atendimentos.Mensagens.Single().Body.Should().Be("Digite seu CPF");
    }
}
