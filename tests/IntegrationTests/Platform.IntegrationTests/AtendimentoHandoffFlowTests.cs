using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Advocacia.Modules.Atendimentos.Api;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;
using Advocacia.Modules.Atendimentos.Application.Features.Bot;
using Advocacia.Modules.Atendimentos.Application.Features.Inbox;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// O handoff ponta a ponta pelo host real (pipeline, JWT do advogado, chave de API do Bot,
/// policies, EF, outbox) contra um Postgres real. A única peça trocada é a saída para o Bot
/// (<see cref="FakeBotGateway"/>) — o contrato dessa chamada é testado no repositório do Bot.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class AtendimentoHandoffFlowTests(PlatformApiFactory factory) : IDisposable
{
    private readonly FakeBotGateway _gateway = new();
    private WebApplicationFactory<Program>? _hostWithFakeBot;

    private WebApplicationFactory<Program> HostWithFakeBot => _hostWithFakeBot ??= factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBotGateway>();
            services.AddSingleton<IBotGateway>(_gateway);
        }));

    public void Dispose() => _hostWithFakeBot?.Dispose();

    // ---- helpers ----

    private static string NewPhone() => "5585" + Random.Shared.NextInt64(900_000_000, 999_999_999);

    private HttpClient Lawyer(WebApplicationFactory<Program> host, Guid tenantId, string role = "Lawyer", Guid? userId = null)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(userId ?? Guid.NewGuid(), "adv@teste.com", tenantId, role));
        return client;
    }

    private static HttpClient Bot(WebApplicationFactory<Program> host, string apiKey)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private async Task<(Guid KeyId, string ApiKey)> IssueKeyAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        var response = await Lawyer(host, tenantId, "Owner").PostAsJsonAsync("/api/v1/bot/api-keys", new IssueBotApiKeyRequestBody("Bot do escritório"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var issued = (await response.Content.ReadAsWebJsonAsync<IssuedBotApiKeyResponse>())!;
        return (issued.Id, issued.ApiKey);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private static async Task<Guid> StartAsync(HttpClient bot, string phone, string? name = "Maria")
    {
        var response = await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = phone, contactName = name });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsWebJsonAsync<StartAtendimentoResponse>())!.AtendimentoId;
    }

    private static Task<HttpResponseMessage> BotMessageAsync(
        HttpClient bot, Guid id, string direction, string sender, string body, string? externalId) =>
        bot.PostAsJsonAsync($"/api/v1/bot/atendimentos/{id}/mensagens", new { direction, sender, body, externalId });

    private static async Task<string> BotStatusAsync(HttpClient bot, Guid id)
    {
        var response = await bot.GetAsync($"/api/v1/bot/atendimentos/{id}/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsWebJsonAsync<AtendimentoStatusResponse>())!.Status;
    }

    private sealed record IssueBotApiKeyRequestBody(string Name);

    // ---- o fluxo completo ----

    [Fact]
    public async Task FullHandoff_ClientAsksForAHuman_LawyerTakesOver_BotGoesSilent_ThenHandsBack()
    {
        var host = HostWithFakeBot;
        var tenant = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        var phone = NewPhone();
        var (_, apiKey) = await IssueKeyAsync(host, tenant);
        var bot = Bot(host, apiKey);
        var lawyer = Lawyer(host, tenant, userId: lawyerId);

        // 1-2. O cliente escreve; o Bot abre o atendimento, guarda a mensagem e responde.
        var id = await StartAsync(bot, phone);
        (await BotMessageAsync(bot, id, "Inbound", "Client", "Olá", "wamid.in.1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BotMessageAsync(bot, id, "Outbound", "Bot", "1 - Prazos | 2 - Falar com humano", "wamid.out.1"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BotStatusAsync(bot, id)).Should().Be("Bot");

        // 3-4. O cliente digita "2": o Bot pede o handoff e avisa o cliente.
        (await BotMessageAsync(bot, id, "Inbound", "Client", "2", "wamid.in.2")).StatusCode.Should().Be(HttpStatusCode.OK);
        var handoff = await bot.PostAsync($"/api/v1/bot/atendimentos/{id}/handoff", null);
        handoff.StatusCode.Should().Be(HttpStatusCode.OK);
        (await handoff.Content.ReadAsWebJsonAsync<HandoffResponse>())!.Success.Should().BeTrue();
        (await BotMessageAsync(bot, id, "Outbound", "Bot", "Aguarde, um advogado vai te atender.", "wamid.out.2"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "o aviso de espera é a única fala do bot permitida em PendingHuman");
        (await BotStatusAsync(bot, id)).Should().Be("PendingHuman");

        // 5. O advogado vê a conversa na fila, aguardando.
        var queue = await (await lawyer.GetAsync("/api/v1/atendimentos?status=PendingHuman"))
            .Content.ReadAsWebJsonAsync<ListAtendimentosResponse>();
        queue!.Items.Should().ContainSingle(item => item.Id == id)
            .Which.Should().Match<AtendimentoResponse>(item =>
                item.Status == "PendingHuman" && item.ContactName == "Maria" && item.PhoneNumber == phone
                && item.LastMessagePreview!.StartsWith("Aguarde"));

        // 6. O advogado assume. A partir daqui o Bot NUNCA pode falar.
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Human" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BotStatusAsync(bot, id)).Should().Be("Human");

        var blocked = await BotMessageAsync(bot, id, "Outbound", "Bot", "Menu", "wamid.out.3");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(blocked)).Should().Be("ATENDIMENTO_BOT_BLOCKED");

        // A mensagem do CLIENTE continua sendo registrada para o advogado ler.
        (await BotMessageAsync(bot, id, "Inbound", "Client", "Doutora, é urgente", "wamid.in.3"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. O advogado responde: o Core entrega ao Bot (não à Meta) com tenant, telefone e texto.
        var sent = await lawyer.PostAsJsonAsync($"/api/v1/atendimentos/{id}/mensagens", new { body = "Bom dia, Maria." });
        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await sent.Content.ReadAsWebJsonAsync<SendMessageResponse>())!.Delivery.Should().Be("Sent");
        _gateway.Sent.Should().ContainSingle().Which.Should().Match<FakeBotGateway.SentMessage>(m =>
            m.TenantId == tenant && m.AtendimentoId == id && m.To == phone && m.Body == "Bom dia, Maria.");

        // 8. O histórico completo, em ordem, com quem disse o quê.
        var history = await (await lawyer.GetAsync($"/api/v1/atendimentos/{id}/mensagens"))
            .Content.ReadAsWebJsonAsync<ListMensagensResponse>();
        history!.Items.Select(m => (m.Sender, m.Body)).Should().Equal(
            ("Client", "Olá"),
            ("Bot", "1 - Prazos | 2 - Falar com humano"),
            ("Client", "2"),
            ("Bot", "Aguarde, um advogado vai te atender."),
            ("Client", "Doutora, é urgente"),
            ("Human", "Bom dia, Maria."));
        history.Items.Last().Delivery.Should().Be("Sent");

        // 9. O advogado devolve ao Bot, que volta a poder falar.
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Bot" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BotStatusAsync(bot, id)).Should().Be("Bot");
        (await BotMessageAsync(bot, id, "Outbound", "Bot", "Posso ajudar em algo mais?", "wamid.out.4"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Encerrar: nova mensagem do mesmo telefone abre OUTRO atendimento.
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Closed" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var again = await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = phone });
        var reopened = (await again.Content.ReadAsWebJsonAsync<StartAtendimentoResponse>())!;
        reopened.Created.Should().BeTrue();
        reopened.AtendimentoId.Should().NotBe(id);
    }

    [Fact]
    public async Task EveryStateChange_LeavesAnImmutableAuditTrail_AndEventsWithoutPersonalData()
    {
        var host = HostWithFakeBot;
        var tenant = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        var phone = NewPhone();
        var bot = Bot(host, (await IssueKeyAsync(host, tenant)).ApiKey);
        var lawyer = Lawyer(host, tenant, userId: lawyerId);

        var id = await StartAsync(bot, phone);
        await BotMessageAsync(bot, id, "Inbound", "Client", "Meu CPF é 111.444.777-35", "wamid.pii.1");
        await bot.PostAsync($"/api/v1/bot/atendimentos/{id}/handoff", null);
        await lawyer.PostAsJsonAsync($"/api/v1/atendimentos/{id}/mensagens", new { body = "Segredo de justiça" });
        await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Bot" });
        await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Closed" });

        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();

        var audit = new List<(string Action, string ActorType, Guid? ActorId)>();
        await using (var command = new NpgsqlCommand(
            "SELECT action, actor_type, actor_id FROM atendimento_auditoria WHERE atendimento_id = @id ORDER BY occurred_at, action", connection))
        {
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                audit.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)));
            }
        }

        audit.Select(a => a.Action).Should().BeEquivalentTo(
            ["Started", "HandoffRequested", "Assigned", "HumanMessageSent", "ReturnedToBot", "Closed"]);
        audit.Single(a => a.Action == "Started").ActorType.Should().Be("Bot");
        audit.Single(a => a.Action == "HandoffRequested").ActorType.Should().Be("Client");
        audit.Where(a => a.Action is "Assigned" or "HumanMessageSent" or "ReturnedToBot" or "Closed")
            .Should().OnlyContain(a => a.ActorType == "User" && a.ActorId == lawyerId);

        // Eventos de domínio: existem na outbox, e NENHUM carrega telefone nem texto da conversa.
        var payloads = new List<string>();
        await using (var command = new NpgsqlCommand(
            "SELECT event_type, payload::text FROM outbox_messages WHERE aggregate_id = @id", connection))
        {
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                payloads.Add($"{reader.GetString(0)} {reader.GetString(1)}");
            }
        }

        payloads.Should().Contain(p => p.Contains("AtendimentoIniciadoEvent"));
        payloads.Should().Contain(p => p.Contains("AtendimentoStatusAlteradoEvent"));
        payloads.Should().Contain(p => p.Contains("AtendimentoMensagemAdicionadaEvent"));
        payloads.Should().NotContain(p => p.Contains(phone) || p.Contains("111.444.777-35") || p.Contains("Segredo de justiça"));
    }

    [Fact]
    public async Task WhenTheBotIsDown_TheLawyersMessageIsKeptAsFailed_AndTheErrorIsReported()
    {
        var host = HostWithFakeBot;
        var tenant = Guid.NewGuid();
        var bot = Bot(host, (await IssueKeyAsync(host, tenant)).ApiKey);
        var lawyer = Lawyer(host, tenant);
        var id = await StartAsync(bot, NewPhone());
        _gateway.FailNext = true;

        var response = await lawyer.PostAsJsonAsync($"/api/v1/atendimentos/{id}/mensagens", new { body = "Está aí?" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await ErrorCodeAsync(response)).Should().Be("ATENDIMENTO_DELIVERY_FAILED");
        var history = await (await lawyer.GetAsync($"/api/v1/atendimentos/{id}/mensagens"))
            .Content.ReadAsWebJsonAsync<ListMensagensResponse>();
        history!.Items.Should().ContainSingle().Which.Should().Match<MensagemResponse>(m =>
            m.Body == "Está aí?" && m.Delivery == "Failed" && m.Sender == "Human");
    }

    [Fact]
    public async Task AnotherLawyer_CannotSpeakInAConversationThatIsAlreadyTaken()
    {
        var host = HostWithFakeBot;
        var tenant = Guid.NewGuid();
        var bot = Bot(host, (await IssueKeyAsync(host, tenant)).ApiKey);
        var id = await StartAsync(bot, NewPhone());

        (await Lawyer(host, tenant).PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Human" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var intruder = await Lawyer(host, tenant).PostAsJsonAsync($"/api/v1/atendimentos/{id}/mensagens", new { body = "Posso?" });

        intruder.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(intruder)).Should().Be("ATENDIMENTO_ALREADY_ASSIGNED");
        _gateway.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Bot_RedeliveredWebhook_IsStoredOnce()
    {
        var host = factory;
        var tenant = Guid.NewGuid();
        var bot = Bot(host, (await IssueKeyAsync(host, tenant)).ApiKey);
        var id = await StartAsync(bot, NewPhone());

        var first = await BotMessageAsync(bot, id, "Inbound", "Client", "Oi", "wamid.dup.1");
        var second = await BotMessageAsync(bot, id, "Inbound", "Client", "Oi", "wamid.dup.1");

        var firstId = (await first.Content.ReadAsWebJsonAsync<AddMessageResponse>())!.MessageId;
        var secondId = (await second.Content.ReadAsWebJsonAsync<AddMessageResponse>())!.MessageId;
        secondId.Should().Be(firstId);
        var history = await (await Lawyer(host, tenant).GetAsync($"/api/v1/atendimentos/{id}/mensagens"))
            .Content.ReadAsWebJsonAsync<ListMensagensResponse>();
        history!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Bot_StartIsIdempotent_AndHandoffCanBeRepeated()
    {
        var host = factory;
        var tenant = Guid.NewGuid();
        var bot = Bot(host, (await IssueKeyAsync(host, tenant)).ApiKey);
        var phone = NewPhone();

        var first = await StartAsync(bot, phone);
        var second = await StartAsync(bot, phone);
        await bot.PostAsync($"/api/v1/bot/atendimentos/{first}/handoff", null);
        var repeated = await bot.PostAsync($"/api/v1/bot/atendimentos/{first}/handoff", null);

        second.Should().Be(first);
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BotStatusAsync(bot, first)).Should().Be("PendingHuman");
    }

    [Theory]
    [InlineData("Inbound", "Human", "x")]
    [InlineData("Sideways", "Client", "x")]
    [InlineData("Inbound", "Client", "")]
    public async Task Bot_RejectsMalformedMessages_With400(string direction, string sender, string body)
    {
        var tenant = Guid.NewGuid();
        var bot = Bot(factory, (await IssueKeyAsync(factory, tenant)).ApiKey);
        var id = await StartAsync(bot, NewPhone());

        var response = await BotMessageAsync(bot, id, direction, sender, body, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bot_RejectsAnInvalidPhone_With400()
    {
        var bot = Bot(factory, (await IssueKeyAsync(factory, Guid.NewGuid())).ApiKey);

        var response = await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = "123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("ATENDIMENTO_PHONE_INVALID");
    }

    // ---- segurança: duas credenciais, dois mundos ----

    [Theory]
    [InlineData("POST", "/api/v1/bot/atendimentos")]
    [InlineData("POST", "/api/v1/bot/atendimentos/00000000-0000-0000-0000-000000000001/mensagens")]
    [InlineData("POST", "/api/v1/bot/atendimentos/00000000-0000-0000-0000-000000000001/handoff")]
    [InlineData("GET", "/api/v1/bot/atendimentos/00000000-0000-0000-0000-000000000001/status")]
    public async Task BotRoutes_WithoutAKey_Return401(string method, string route)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BotRoutes_WithAnUnknownKey_Return401()
    {
        var bot = Bot(factory, "adv_bot_esta-chave-nao-existe");

        (await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = NewPhone() }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BotRoutes_WithARevokedKey_Return401()
    {
        var tenant = Guid.NewGuid();
        var (keyId, apiKey) = await IssueKeyAsync(factory, tenant);
        var bot = Bot(factory, apiKey);
        (await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = NewPhone() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await Lawyer(factory, tenant, "Owner").DeleteAsync($"/api/v1/bot/api-keys/{keyId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = NewPhone() }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a rotação só vale se a chave antiga morre de verdade");
    }

    [Fact]
    public async Task ALawyersJwt_CannotCallTheBotApi()
    {
        var response = await Lawyer(factory, Guid.NewGuid(), "Owner")
            .PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = NewPhone() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/v1/atendimentos")]
    [InlineData("/api/v1/bot/api-keys")]
    public async Task TheBotKey_CannotCallTheLawyersApi(string route)
    {
        var bot = Bot(factory, (await IssueKeyAsync(factory, Guid.NewGuid())).ApiKey);

        (await bot.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnIntern_CannotReadClientConversations()
    {
        var response = await Lawyer(factory, Guid.NewGuid(), "Intern").GetAsync("/api/v1/atendimentos");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Lawyer")]
    [InlineData("Partner")]
    public async Task OnlyTheOwner_CanIssueBotKeys(string role)
    {
        var response = await Lawyer(factory, Guid.NewGuid(), role)
            .PostAsJsonAsync("/api/v1/bot/api-keys", new IssueBotApiKeyRequestBody("x"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListingKeys_NeverExposesTheSecret()
    {
        var tenant = Guid.NewGuid();
        var (_, apiKey) = await IssueKeyAsync(factory, tenant);

        var body = await (await Lawyer(factory, tenant, "Owner").GetAsync("/api/v1/bot/api-keys")).Content.ReadAsStringAsync();

        body.Should().Contain("adv_bot_").And.NotContain(apiKey);
        body.Should().NotContain("keyHash", "nem o hash sai da API");
    }

    // ---- isolamento entre escritórios ----

    [Fact]
    public async Task OneOfficeNeverSeesTheOthersConversations()
    {
        var host = HostWithFakeBot;
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var phone = NewPhone();
        var botA = Bot(host, (await IssueKeyAsync(host, tenantA)).ApiKey);
        var botB = Bot(host, (await IssueKeyAsync(host, tenantB)).ApiKey);
        var idA = await StartAsync(botA, phone);
        var lawyerB = Lawyer(host, tenantB);

        // O Bot do escritório B não enxerga o atendimento de A, nem o confunde com o seu.
        (await botB.GetAsync($"/api/v1/bot/atendimentos/{idA}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await botB.PostAsync($"/api/v1/bot/atendimentos/{idA}/handoff", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BotMessageAsync(botB, idA, "Inbound", "Client", "x", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var idB = await StartAsync(botB, phone);
        idB.Should().NotBe(idA, "o mesmo telefone em dois escritórios são duas conversas");

        // O advogado de B tampouco.
        var listed = await (await lawyerB.GetAsync("/api/v1/atendimentos?pageSize=100")).Content.ReadAsWebJsonAsync<ListAtendimentosResponse>();
        listed!.Items.Select(i => i.Id).Should().Contain(idB).And.NotContain(idA);
        (await lawyerB.GetAsync($"/api/v1/atendimentos/{idA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lawyerB.GetAsync($"/api/v1/atendimentos/{idA}/mensagens")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lawyerB.PatchAsJsonAsync($"/api/v1/atendimentos/{idA}/status", new { status = "Closed" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lawyerB.PostAsJsonAsync($"/api/v1/atendimentos/{idA}/mensagens", new { body = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        _gateway.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Inbox_FiltersByStatusAndSearchesByNameOrPhone()
    {
        var tenant = Guid.NewGuid();
        var bot = Bot(factory, (await IssueKeyAsync(factory, tenant)).ApiKey);
        var lawyer = Lawyer(factory, tenant);
        var maria = await StartAsync(bot, "5585911112222", "Maria Souza");
        var joao = await StartAsync(bot, "5585933334444", "João Pereira");
        await bot.PostAsync($"/api/v1/bot/atendimentos/{joao}/handoff", null);

        async Task<IReadOnlyList<Guid>> Ids(string query) =>
            (await (await lawyer.GetAsync($"/api/v1/atendimentos{query}")).Content.ReadAsWebJsonAsync<ListAtendimentosResponse>())!
            .Items.Select(i => i.Id).ToList();

        (await Ids("?status=PendingHuman")).Should().Equal(joao);
        (await Ids("?status=Bot")).Should().Equal(maria);
        (await Ids("?search=souza")).Should().Equal(maria);
        (await Ids("?search=PEREI")).Should().Equal(joao);
        (await Ids("?search=3333")).Should().Equal(joao);
        (await Ids("")).Should().Equal(joao, maria);
        (await lawyer.GetAsync("/api/v1/atendimentos?status=Nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inbox_SearchTreatsWildcardCharactersLiterally()
    {
        var tenant = Guid.NewGuid();
        var bot = Bot(factory, (await IssueKeyAsync(factory, tenant)).ApiKey);
        await StartAsync(bot, "5585911112222", "Maria Souza");

        var response = await Lawyer(factory, tenant).GetAsync("/api/v1/atendimentos?search=%25");

        (await response.Content.ReadAsWebJsonAsync<ListAtendimentosResponse>())!.Items.Should().BeEmpty(
            "'%' digitado pelo usuário não pode virar curinga e listar tudo");
    }

    [Fact]
    public async Task Lawyer_CanLinkTheConversationToAClient()
    {
        var tenant = Guid.NewGuid();
        var bot = Bot(factory, (await IssueKeyAsync(factory, tenant)).ApiKey);
        var lawyer = Lawyer(factory, tenant);
        var id = await StartAsync(bot, NewPhone());
        var clienteId = Guid.NewGuid();

        (await lawyer.PutAsJsonAsync($"/api/v1/atendimentos/{id}/cliente", new { clienteId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await lawyer.GetAsync($"/api/v1/atendimentos/{id}")).Content.ReadAsWebJsonAsync<AtendimentoResponse>();
        detail!.ClientId.Should().Be(clienteId);
    }
}
