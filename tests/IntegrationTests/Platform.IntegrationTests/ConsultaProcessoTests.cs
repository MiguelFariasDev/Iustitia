using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;
using Advocacia.Modules.Atendimentos.Application.Features.Bot;
using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// A consulta de andamento pelo bot de ponta a ponta: host real, Legal real, PostgreSQL real.
/// O foco é o que NÃO pode acontecer — ver processo de outro cliente ou de outro escritório, passar
/// pela verificação sem o documento, e o documento aparecer em algum lugar.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class ConsultaProcessoTests(PlatformApiFactory factory)
{
    // CPFs com dígitos verificadores válidos (o cadastro de cliente os valida).
    private const string CpfMaria = "111.444.777-35";
    private const string CpfJoao = "529.982.247-25";
    private const string CpfAna = "168.995.350-09";

    // ---------------------------------------------------------------- cenário

    private sealed record Office(Guid Tenant, string BotKey, HttpClient Owner, HttpClient Lawyer);

    private sealed record Cliente(Guid Id, string Cpf, string Telefone);

    private static string NewNationalPhone() => "859" + Random.Shared.Next(10_000_000, 99_999_999);

    private static string WaPhone(string national) => "55" + national;

    private HttpClient Jwt(WebApplicationFactory<Program> host, Guid tenant, string role)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), $"{role}@t.com", tenant, role));
        return client;
    }

    private async Task<Office> NewOfficeAsync(WebApplicationFactory<Program>? host = null)
    {
        host ??= factory;
        var tenant = Guid.NewGuid();
        var owner = Jwt(host, tenant, "Owner");
        var issued = await (await owner.PostAsJsonAsync("/api/v1/bot/api-keys", new { name = "Bot" }))
            .Content.ReadAsWebJsonAsync<IssuedBotApiKeyResponse>();
        return new Office(tenant, issued!.ApiKey, owner, Jwt(host, tenant, "Lawyer"));
    }

    private static HttpClient BotOf(WebApplicationFactory<Program> host, Office office)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", office.BotKey);
        return client;
    }

    private static async Task<Cliente> NewClienteAsync(Office office, string nome, string cpf, string? telefone = null)
    {
        telefone ??= NewNationalPhone();
        var response = await office.Lawyer.PostAsJsonAsync(
            "/api/v1/clientes", new CreateClienteCommand("PessoaFisica", nome, cpf, new ContatoRequest(null, telefone)));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return new Cliente((await response.Content.ReadAsWebJsonAsync<ClienteResponse>())!.Id, cpf, telefone);
    }

    /// <summary>Número CNJ válido (dígito verificador pelo módulo 97 da Res. 65/CNJ).</summary>
    private static string NewCnj()
    {
        var sequential = Random.Shared.Next(1, 9_999_999).ToString("D7");
        var rest = "20268" + "26" + "0001";
        var remainder = (int)(System.Numerics.BigInteger.Parse(sequential + rest + "00") % 97);
        var dv = (98 - remainder).ToString("D2");
        var cnj = $"{sequential}-{dv}.2026.8.26.0001";
        CNJNumber.Create(cnj).IsSuccess.Should().BeTrue($"o gerador de CNJ de teste deve produzir números válidos: {cnj}");
        return cnj;
    }

    private static async Task<(Guid Id, string Cnj)> NewProcessoAsync(Office office, Cliente cliente, string assunto)
    {
        var cnj = NewCnj();
        var response = await office.Lawyer.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(cnj, cliente.Id, "Civel", Assunto: assunto, ParteContraria: "PARTE CONTRARIA SIGILOSA", ValorCausa: 987654.32m, Observacoes: "NOTA INTERNA CONFIDENCIAL"));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return ((await response.Content.ReadAsWebJsonAsync<ProcessoResponse>())!.Id, cnj);
    }

    private async Task SeedPublicationAsync(
        Office office, Guid processId, Guid clienteId, string cnj, PublicationType type, PublicationStatus status, int daysAgo, string rawText)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(office.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<LegalDbContext>();

        var publication = Publication.Create(
            office.Tenant, CNJNumber.Create(cnj).Value, $"ext-{Guid.NewGuid():N}", PublicationSource.DJEN,
            RawContent.Create(rawText).Value,
            PublishedAt.Create(DateTimeOffset.UtcNow.AddDays(-daysAgo), DateTimeOffset.UtcNow).Value,
            DateTimeOffset.UtcNow, type).Value;
        publication.LinkTo(processId, clienteId, null).IsSuccess.Should().BeTrue();
        if (status != PublicationStatus.PendingReview)
        {
            publication.ChangeStatus(status, "revisada").IsSuccess.Should().BeTrue();
        }

        db.Publications.Add(publication);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> StartAtendimentoAsync(HttpClient bot, string waPhone)
    {
        var response = await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = waPhone, contactName = "Contato" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsWebJsonAsync<StartAtendimentoResponse>())!.AtendimentoId;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> PostAsync(HttpClient client, string url, object? body = null)
    {
        var response = await client.PostAsJsonAsync(url, body ?? new { });
        var raw = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, raw.Length > 0 ? JsonDocument.Parse(raw).RootElement.Clone() : default, raw);
    }

    private static Task<(HttpStatusCode Status, JsonElement Body, string Raw)> IniciarAsync(HttpClient bot, Guid id) =>
        PostAsync(bot, $"/api/v1/bot/atendimentos/{id}/consulta/iniciar");

    private static Task<(HttpStatusCode Status, JsonElement Body, string Raw)> VerificarAsync(HttpClient bot, Guid id, string documento) =>
        PostAsync(bot, $"/api/v1/bot/atendimentos/{id}/consulta/verificar", new { documento });

    private static async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> ProcessoAsync(HttpClient bot, Guid id, int indice)
    {
        var response = await bot.GetAsync($"/api/v1/bot/atendimentos/{id}/consulta/processos/{indice}");
        var raw = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, raw.Length > 0 ? JsonDocument.Parse(raw).RootElement.Clone() : default, raw);
    }

    private static string Etapa(JsonElement body) => body.GetProperty("etapa").GetString()!;

    private static string ErrorCode(JsonElement body) => body.GetProperty("errorCode").GetString()!;

    private static async Task<List<string>> DumpAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    // ---------------------------------------------------------------- o caminho feliz

    [Fact]
    public async Task HappyPath_StartVerifyListAndOpenAProcess()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var (processId, cnj) = await NewProcessoAsync(office, maria, "Ação de cobrança");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Sentenca, PublicationStatus.Approved, 3, "INTEIRO TEOR SIGILOSO DA SENTENÇA");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));

        var iniciar = await IniciarAsync(bot, atendimento);
        var wrong = await VerificarAsync(bot, atendimento, "000.000.000-00");
        var right = await VerificarAsync(bot, atendimento, CpfMaria);
        var detalhe = await ProcessoAsync(bot, atendimento, 1);

        iniciar.Status.Should().Be(HttpStatusCode.OK);
        Etapa(iniciar.Body).Should().Be("AguardandoDocumento");
        Etapa(wrong.Body).Should().Be("Negado");
        wrong.Raw.Should().NotContain("processos");
        Etapa(right.Body).Should().Be("Verificado");
        right.Body.GetProperty("processos").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("titulo").GetString().Should().Be($"Ação de cobrança · {cnj}");
        right.Body.GetProperty("processos")[0].GetProperty("indice").GetInt32().Should().Be(1);

        detalhe.Status.Should().Be(HttpStatusCode.OK);
        detalhe.Body.GetProperty("titulo").GetString().Should().Be($"Ação de cobrança · {cnj}");
        detalhe.Body.GetProperty("fase").GetString().Should().Be("Em andamento");
        detalhe.Body.GetProperty("ultimoAndamento").GetString().Should().StartWith("Sentença publicada em ");
        detalhe.Body.GetProperty("atualizadoEm").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddDays(-4));
    }

    [Theory]
    [InlineData("111.444.777-35")]
    [InlineData("111 444 777 35")]
    [InlineData("11144477735")]
    [InlineData("  111.444.777-35  ")]
    public async Task ThePunctuationOfTheDocumentDoesNotMatter(string typed)
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);

        Etapa((await VerificarAsync(bot, atendimento, typed)).Body).Should().Be("Verificado");
    }

    [Fact]
    public async Task TheNinthDigitVariation_StillFindsTheClient()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria, "85988887777");
        var bot = BotOf(factory, office);

        // O WhatsApp entregou o número do celular antigo SEM o nono dígito.
        var atendimento = await StartAtendimentoAsync(bot, "558588887777");

        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("AguardandoDocumento");
    }

    [Fact]
    public async Task ThePhaseAndLastMovement_AreInPlainLanguage_AndASuspendedProcessSaysSo()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var (processId, _) = await NewProcessoAsync(office, maria, "Inventário");
        (await office.Lawyer.PatchAsJsonAsync($"/api/v1/processos/{processId}/status", new { status = "Suspenso" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        await VerificarAsync(bot, atendimento, CpfMaria);

        var detalhe = await ProcessoAsync(bot, atendimento, 1);

        detalhe.Body.GetProperty("fase").GetString().Should().Be("Suspenso");
        detalhe.Body.GetProperty("ultimoAndamento").GetString().Should().Be("Ainda não há movimentações confirmadas pelo escritório.");
    }

    // ---------------------------------------------------------------- o que NUNCA pode sair

    [Fact]
    public async Task OnlyTitlePhaseLastMovementAndDate_NeverPiecesValuesOpposingPartyOrInternalNotes()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var (processId, cnj) = await NewProcessoAsync(office, maria, "Ação de cobrança");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Despacho, PublicationStatus.Approved, 2, "TEXTO INTEGRAL DO DESPACHO SIGILOSO");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        var list = await VerificarAsync(bot, atendimento, CpfMaria);
        var detail = await ProcessoAsync(bot, atendimento, 1);

        detail.Body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["titulo", "fase", "ultimoAndamento", "atualizadoEm"]);
        foreach (var raw in new[] { list.Raw, detail.Raw })
        {
            raw.Should().NotContain("INTEIRO TEOR").And.NotContain("TEXTO INTEGRAL");
            raw.Should().NotContain("PARTE CONTRARIA SIGILOSA").And.NotContain("NOTA INTERNA CONFIDENCIAL");
            raw.Should().NotContain("987654").And.NotContain("987.654");
            raw.Should().NotContain(maria.Telefone).And.NotContain("11144477735").And.NotContain(CpfMaria);
        }
    }

    [Fact]
    public async Task OnlyReviewedMovements_AreShown_ATentativeClassificationIsNotToldToTheClient()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var (processId, cnj) = await NewProcessoAsync(office, maria, "Ação de cobrança");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Despacho, PublicationStatus.Approved, 10, "x");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Sentenca, PublicationStatus.PendingReview, 1, "y");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Acordao, PublicationStatus.Rejected, 0, "z");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        await VerificarAsync(bot, atendimento, CpfMaria);

        var detail = await ProcessoAsync(bot, atendimento, 1);

        detail.Body.GetProperty("ultimoAndamento").GetString().Should().StartWith("Despacho do juiz publicada em ")
            .And.NotContain("Sentença").And.NotContain("Acórdão");
    }

    [Fact]
    public async Task ArchivedProcesses_AreNotListed()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        await NewProcessoAsync(office, maria, "Ativo");
        var (archivedId, _) = await NewProcessoAsync(office, maria, "Arquivado");
        (await Jwt(factory, office.Tenant, "Partner").PostAsync($"/api/v1/processos/{archivedId}/archive", null)).IsSuccessStatusCode.Should().BeTrue();
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);

        var list = await VerificarAsync(bot, atendimento, CpfMaria);

        list.Body.GetProperty("processos").EnumerateArray().Select(p => p.GetProperty("titulo").GetString())
            .Should().ContainSingle().Which.Should().StartWith("Ativo");
    }

    // ---------------------------------------------------------------- isolamento

    [Fact]
    public async Task AClientNeverSeesAnotherClientsProcess_EvenInTheSameOffice()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var joao = await NewClienteAsync(office, "João Pereira", CpfJoao);
        var (_, cnjMaria) = await NewProcessoAsync(office, maria, "Processo da Maria");
        var (_, cnjJoao) = await NewProcessoAsync(office, joao, "Processo do João");
        var bot = BotOf(factory, office);

        // Maria verifica com o PRÓPRIO documento, mas o telefone dela só abre o cadastro dela.
        var atendimentoMaria = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimentoMaria);
        var list = await VerificarAsync(bot, atendimentoMaria, CpfMaria);

        list.Raw.Should().Contain("Processo da Maria").And.NotContain("Processo do João").And.NotContain(cnjJoao);
        (await ProcessoAsync(bot, atendimentoMaria, 1)).Raw.Should().NotContain("João").And.NotContain(cnjJoao);
        (await ProcessoAsync(bot, atendimentoMaria, 2)).Status.Should().Be(HttpStatusCode.NotFound);

        // E o documento do João, no telefone da Maria, não abre nada.
        await IniciarAsync(bot, atendimentoMaria);
        var withJoaosDocument = await VerificarAsync(bot, atendimentoMaria, CpfJoao);
        Etapa(withJoaosDocument.Body).Should().Be("Negado");
        withJoaosDocument.Raw.Should().NotContain(cnjMaria).And.NotContain(cnjJoao);
    }

    [Fact]
    public async Task AClientNeverSeesAProcessOfAnotherOffice_EvenWithTheSamePhoneAndTheSameDocument()
    {
        var officeA = await NewOfficeAsync();
        var officeB = await NewOfficeAsync();
        var phone = NewNationalPhone();
        var mariaA = await NewClienteAsync(officeA, "Maria no A", CpfMaria, phone);
        var mariaB = await NewClienteAsync(officeB, "Maria no B", CpfMaria, phone);
        await NewProcessoAsync(officeA, mariaA, "Processo do escritório A");
        var (_, cnjB) = await NewProcessoAsync(officeB, mariaB, "Processo do escritório B");
        var botA = BotOf(factory, officeA);
        var botB = BotOf(factory, officeB);

        var atendimentoA = await StartAtendimentoAsync(botA, WaPhone(phone));
        await IniciarAsync(botA, atendimentoA);
        var listA = await VerificarAsync(botA, atendimentoA, CpfMaria);

        var atendimentoB = await StartAtendimentoAsync(botB, WaPhone(phone));
        await IniciarAsync(botB, atendimentoB);
        var listB = await VerificarAsync(botB, atendimentoB, CpfMaria);

        listA.Raw.Should().Contain("escritório A").And.NotContain("escritório B").And.NotContain(cnjB);
        listB.Raw.Should().Contain("escritório B").And.NotContain("escritório A");
        // O atendimento do outro escritório nem é visível para a chave do primeiro.
        (await IniciarAsync(botA, atendimentoB)).Status.Should().Be(HttpStatusCode.NotFound);
        (await VerificarAsync(botA, atendimentoB, CpfMaria)).Status.Should().Be(HttpStatusCode.NotFound);
        (await ProcessoAsync(botA, atendimentoB, 1)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task APhoneRegisteredOnlyInAnotherOffice_IsDenied()
    {
        var officeA = await NewOfficeAsync();
        var officeB = await NewOfficeAsync();
        var maria = await NewClienteAsync(officeA, "Maria", CpfMaria);
        var botB = BotOf(factory, officeB);
        var atendimentoB = await StartAtendimentoAsync(botB, WaPhone(maria.Telefone));

        var result = await IniciarAsync(botB, atendimentoB);

        Etapa(result.Body).Should().Be("Negado");
        result.Raw.Should().NotContain("Maria");
    }

    [Fact]
    public async Task ASharedPhone_IdentifiesNobody()
    {
        var office = await NewOfficeAsync();
        var phone = NewNationalPhone();
        await NewClienteAsync(office, "Mãe", CpfMaria, phone);
        await NewClienteAsync(office, "Filho", CpfJoao, phone);
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(phone));

        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("Negado");
    }

    [Fact]
    public async Task AnInactiveClient_CannotBeVerified()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var partner = Jwt(factory, office.Tenant, "Partner");
        (await partner.DeleteAsync($"/api/v1/clientes/{maria.Id}")).IsSuccessStatusCode.Should().BeTrue();
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));

        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("Negado");
    }

    [Fact]
    public async Task UnknownPhone_AndWrongDocument_AreIndistinguishableOnTheWire()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var bot = BotOf(factory, office);
        var stranger = await StartAtendimentoAsync(bot, WaPhone(NewNationalPhone()));
        var known = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, known);

        var unknownPhone = await IniciarAsync(bot, stranger);
        var wrongDocument = await VerificarAsync(bot, known, "000.000.000-00");

        unknownPhone.Status.Should().Be(wrongDocument.Status);
        unknownPhone.Raw.Should().Be(wrongDocument.Raw, "mesmo status e mesmo corpo: não revela se o número é de um cliente");
    }

    // ---------------------------------------------------------------- só com o bot

    [Theory]
    [InlineData("PendingHuman")]
    [InlineData("Human")]
    [InlineData("Closed")]
    public async Task OutsideTheBotStatus_EveryEndpointIs409(string status)
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        await NewProcessoAsync(office, maria, "Ação");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        await VerificarAsync(bot, atendimento, CpfMaria);

        switch (status)
        {
            case "PendingHuman":
                (await bot.PostAsync($"/api/v1/bot/atendimentos/{atendimento}/handoff", null)).IsSuccessStatusCode.Should().BeTrue();
                break;
            case "Human":
                (await office.Lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{atendimento}/status", new { status = "Human" })).IsSuccessStatusCode.Should().BeTrue();
                break;
            default:
                (await office.Lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{atendimento}/status", new { status = "Closed" })).IsSuccessStatusCode.Should().BeTrue();
                break;
        }

        var results = new[]
        {
            await IniciarAsync(bot, atendimento),
            await VerificarAsync(bot, atendimento, CpfMaria),
            await ProcessoAsync(bot, atendimento, 1),
        };

        foreach (var result in results)
        {
            result.Status.Should().Be(HttpStatusCode.Conflict);
            ErrorCode(result.Body).Should().Be("ATENDIMENTO_CONSULTA_REQUER_BOT");
            result.Raw.Should().NotContain("Ação", "mesmo com a sessão verificada antes, nada sai depois que o humano assume");
        }
    }

    // ---------------------------------------------------------------- bloqueio e expiração

    [Fact]
    public async Task ThreeFailures_Block_AndTheRightDocumentIsRefusedWhileBlocked()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        await NewProcessoAsync(office, maria, "Ação");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);

        var etapas = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            etapas.Add(Etapa((await VerificarAsync(bot, atendimento, "000.000.000-0" + i)).Body));
        }

        etapas.Should().Equal("Negado", "Negado", "Bloqueado");
        var right = await VerificarAsync(bot, atendimento, CpfMaria);
        Etapa(right.Body).Should().Be("Bloqueado");
        right.Raw.Should().NotContain("processos");
        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("Bloqueado", "recomeçar não desbloqueia");
    }

    [Fact]
    public async Task TheBlock_FollowsTheClientToANewConversation()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var bot = BotOf(factory, office);
        var first = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, first);
        for (var i = 0; i < 3; i++)
        {
            await VerificarAsync(bot, first, "000.000.000-00");
        }

        (await office.Lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{first}/status", new { status = "Closed" })).IsSuccessStatusCode.Should().BeTrue();
        var second = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        second.Should().NotBe(first);

        Etapa((await IniciarAsync(bot, second)).Body).Should().Be("Bloqueado");
    }

    [Fact]
    public async Task RestartingInALoop_DoesNotGiveMoreThanThreeGuesses()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        var compared = 0;

        for (var round = 0; round < 6; round++)
        {
            var started = await IniciarAsync(bot, atendimento);
            if (Etapa(started.Body) == "Bloqueado")
            {
                break;
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var result = await VerificarAsync(bot, atendimento, $"000.000.000-{round}{attempt}");
                if (Etapa(result.Body) != "Bloqueado")
                {
                    compared++;
                }
            }
        }

        compared.Should().BeLessThanOrEqualTo(2 + 0, "no máximo 2 palpites erram sem bloquear; o terceiro bloqueia");
        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("Bloqueado");
    }

    [Fact]
    public async Task AnExpiredSession_ReportsExpired_AndTheDetailIsForbidden()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        await NewProcessoAsync(office, maria, "Ação");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        await VerificarAsync(bot, atendimento, CpfMaria);

        await using (var connection = new NpgsqlConnection(factory.SuperuserConnectionString))
        {
            await connection.OpenAsync();
            await using var expire = new NpgsqlCommand(
                "UPDATE atendimento_consulta_sessoes SET expires_at = now() - interval '1 second' WHERE atendimento_id = @id", connection);
            expire.Parameters.AddWithValue("id", atendimento);
            await expire.ExecuteNonQueryAsync();
        }

        var verify = await VerificarAsync(bot, atendimento, CpfMaria);
        var detail = await ProcessoAsync(bot, atendimento, 1);

        Etapa(verify.Body).Should().Be("Expirada");
        detail.Status.Should().Be(HttpStatusCode.Forbidden);
        ErrorCode(detail.Body).Should().Be("ATENDIMENTO_CONSULTA_NAO_VERIFICADA");
        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("AguardandoDocumento", "iniciar de novo recomeça");
    }

    [Fact]
    public async Task WithoutVerification_TheDetailIsForbidden_AndTheProcessIsNotEvenLooked()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        await NewProcessoAsync(office, maria, "Ação secreta");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));

        var beforeStart = await ProcessoAsync(bot, atendimento, 1);
        await IniciarAsync(bot, atendimento);
        var beforeVerifying = await ProcessoAsync(bot, atendimento, 1);

        foreach (var result in new[] { beforeStart, beforeVerifying })
        {
            result.Status.Should().Be(HttpStatusCode.Forbidden);
            result.Raw.Should().NotContain("Ação secreta");
        }
    }

    [Fact]
    public async Task ParallelGuesses_CannotBeatTheLimit()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => VerificarAsync(bot, atendimento, $"000.000.00{i % 10}-0{i % 7}")));

        responses.Should().NotContain(r => r.Status == HttpStatusCode.OK && Etapa(r.Body) == "Verificado");
        var failed = await DumpAsync(factory.SuperuserConnectionString,
            $"SELECT failed_attempts::text FROM atendimento_consulta_sessoes WHERE atendimento_id = '{atendimento}'");
        int.Parse(failed.Single()).Should().BeLessThanOrEqualTo(3, "o token de concorrência impede contar além do limite — e impede avaliar palpites a mais");

        // Sem o token de concorrência, os 20 pedidos leriam "0 falhas" ao mesmo tempo e TODOS responderiam
        // "Negado" — 20 palpites avaliados. Com ele só os dois primeiros podem ser "Negado" (o terceiro bloqueia),
        // e os pedidos que perdem a corrida falham em vez de ganhar um palpite de graça.
        responses.Count(r => r.Status == HttpStatusCode.OK && Etapa(r.Body) == "Negado")
            .Should().BeLessThanOrEqualTo(2);
    }

    // ---------------------------------------------------------------- conectado como app_user (produção)

    [Fact]
    public async Task TheWholeFlowWorks_UnderRlsAndMinimalGrants_AsAppUser()
    {
        // Outros hosts desta collection rodam como dono das tabelas e criam o schema do Hangfire; em
        // produção só existe um role. Libera o schema para o app_user antes de subir o host dele.
        await using (var admin = new NpgsqlConnection(factory.SuperuserConnectionString))
        {
            await admin.OpenAsync();
            await using var grant = new NpgsqlCommand(
                """
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'hangfire') THEN
                    GRANT ALL ON SCHEMA hangfire TO app_user;
                    GRANT ALL ON ALL TABLES IN SCHEMA hangfire TO app_user;
                    GRANT ALL ON ALL SEQUENCES IN SCHEMA hangfire TO app_user;
                  END IF;
                END $$;
                """, admin);
            await grant.ExecuteNonQueryAsync();
        }

        using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:DefaultConnection", factory.AppUserConnectionString));
        var office = await NewOfficeAsync(host);
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var joao = await NewClienteAsync(office, "João Pereira", CpfJoao);
        var (processId, cnj) = await NewProcessoAsync(office, maria, "Ação da Maria");
        await NewProcessoAsync(office, joao, "Ação do João");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Intimacao, PublicationStatus.Approved, 1, "x");
        var bot = BotOf(host, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));

        Etapa((await IniciarAsync(bot, atendimento)).Body).Should().Be("AguardandoDocumento");
        Etapa((await VerificarAsync(bot, atendimento, "000.000.000-00")).Body).Should().Be("Negado");
        var verified = await VerificarAsync(bot, atendimento, CpfMaria);
        var detail = await ProcessoAsync(bot, atendimento, 1);

        Etapa(verified.Body).Should().Be("Verificado");
        verified.Raw.Should().Contain("Ação da Maria").And.NotContain("Ação do João");
        detail.Body.GetProperty("ultimoAndamento").GetString().Should().StartWith("Intimação publicada em ");

        // Bloqueio sob os mesmos privilégios: 3 falhas numa conversa nova do mesmo cliente.
        var second = Jwt(host, office.Tenant, "Lawyer");
        (await second.PatchAsJsonAsync($"/api/v1/atendimentos/{atendimento}/status", new { status = "Closed" })).IsSuccessStatusCode.Should().BeTrue();
        var fresh = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, fresh);
        for (var i = 0; i < 3; i++)
        {
            await VerificarAsync(bot, fresh, "000.000.000-00");
        }

        Etapa((await VerificarAsync(bot, fresh, CpfMaria)).Body).Should().Be("Bloqueado");
    }

    // ---------------------------------------------------------------- o documento não vaza

    [Fact]
    public async Task TheDocumentIsNeverStored_NotInTheHistory_TheAudit_TheSessionsNorTheOutbox()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
        var (processId, cnj) = await NewProcessoAsync(office, maria, "Ação de cobrança");
        await SeedPublicationAsync(office, processId, maria.Id, cnj, PublicationType.Sentenca, PublicationStatus.Approved, 1, "t");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);

        // O Bot registra a mensagem do cliente (com o CPF digitado) como qualquer outra...
        async Task RecordInboundAsync(string text, string externalId) =>
            (await bot.PostAsJsonAsync($"/api/v1/bot/atendimentos/{atendimento}/mensagens",
                new { direction = "Inbound", sender = "Client", body = text, externalId })).StatusCode.Should().Be(HttpStatusCode.OK);

        await RecordInboundAsync("000.000.000-00", "wamid.errado");
        await VerificarAsync(bot, atendimento, "000.000.000-00");
        await RecordInboundAsync(CpfMaria, "wamid.certo");
        await VerificarAsync(bot, atendimento, CpfMaria);
        await ProcessoAsync(bot, atendimento, 1);

        // ...e o Core a oculta. Procura o documento (formatado e só dígitos) em tudo que o módulo grava.
        var digits = "11144477735";
        var wrongDigits = "00000000000";
        var tables = new[]
        {
            "atendimento_mensagens", "atendimento_auditoria", "atendimento_consulta_sessoes", "atendimentos",
        };
        foreach (var table in tables)
        {
            var rows = await DumpAsync(factory.SuperuserConnectionString,
                $"SELECT row_to_json(t)::text FROM {table} t WHERE tenant_id = '{office.Tenant}'");
            rows.Should().NotBeEmpty($"{table} deveria ter linhas deste cenário");
            string.Join('\n', rows).Should().NotContain(CpfMaria).And.NotContain(digits).And.NotContain("000.000.000-00").And.NotContain(wrongDigits, table);
        }

        var outbox = await DumpAsync(factory.SuperuserConnectionString, "SELECT payload::text FROM outbox_messages");
        string.Join('\n', outbox).Should().NotContain(CpfMaria).And.NotContain(digits);

        var history = await DumpAsync(factory.SuperuserConnectionString,
            $"SELECT body FROM atendimento_mensagens WHERE atendimento_id = '{atendimento}' ORDER BY created_at");
        history.Should().OnlyContain(body => body == "[mensagem omitida: verificação de identidade]");
    }

    [Fact]
    public async Task TheAuditTrail_RecordsWhoConsultedWhatAndWhen_WithTheResult_ButNotTheDocument()
    {
        var office = await NewOfficeAsync();
        var maria = await NewClienteAsync(office, "Maria", CpfMaria);
        var (processId, _) = await NewProcessoAsync(office, maria, "Ação");
        var bot = BotOf(factory, office);
        var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
        await IniciarAsync(bot, atendimento);
        await VerificarAsync(bot, atendimento, "000.000.000-00");
        await VerificarAsync(bot, atendimento, CpfMaria);
        await ProcessoAsync(bot, atendimento, 1);

        var rows = await DumpAsync(factory.SuperuserConnectionString,
            $"SELECT action || '|' || actor_type || '|' || coalesce(actor_id::text,'') || '|' || coalesce(reference_id::text,'') FROM atendimento_auditoria " +
            $"WHERE atendimento_id = '{atendimento}' AND action LIKE 'Consulta%' ORDER BY occurred_at, action");

        rows.Should().HaveCount(4);
        rows.Select(r => r.Split('|')[0]).Should().BeEquivalentTo(
            ["ConsultaIniciada", "ConsultaNegada", "ConsultaVerificada", "ConsultaProcessoVisualizado"]);
        rows.Should().OnlyContain(r => r.Split('|')[1] == "Client" && r.Split('|')[2] == maria.Id.ToString());
        rows.Single(r => r.StartsWith("ConsultaProcessoVisualizado")).Should().EndWith(processId.ToString());
    }

    [Fact]
    public async Task NothingSensitiveReachesTheApplicationLogs()
    {
        Office office;
        Cliente maria;
        string cnj;
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));

        try
        {
            // Host novo: o sink do Console do Serilog captura o Console.Out vigente na criação.
            using var host = factory.WithWebHostBuilder(_ => { });
            office = await NewOfficeAsync(host);
            maria = await NewClienteAsync(office, "Maria Souza", CpfMaria);
            var (processId, cnj0) = await NewProcessoAsync(office, maria, "Assunto reservado XYZ");
            cnj = cnj0;
            await SeedPublicationAsync(office, processId, maria.Id, cnj0, PublicationType.Sentenca, PublicationStatus.Approved, 1, "TEOR ABC");
            var bot = BotOf(host, office);
            var atendimento = await StartAtendimentoAsync(bot, WaPhone(maria.Telefone));
            await IniciarAsync(bot, atendimento);
            await VerificarAsync(bot, atendimento, "999.999.999-99");
            await VerificarAsync(bot, atendimento, CpfMaria);
            await ProcessoAsync(bot, atendimento, 1);
            // Um pedido inválido, para exercitar o caminho de erro de validação.
            await VerificarAsync(bot, atendimento, new string('7', 40));
        }
        finally
        {
            Console.SetOut(original);
        }

        var logs = captured.ToString();
        logs.Should().Contain("VerificarConsultaCommand", "o teste só vale se o host de fato logou a requisição");
        logs.Should().NotContain("11144477735").And.NotContain("111.444.777-35").And.NotContain("99999999999").And.NotContain("999.999.999-99");
        logs.Should().NotContain("7777777777");
        logs.Should().NotContain(maria.Telefone).And.NotContain(cnj).And.NotContain("Assunto reservado XYZ").And.NotContain("TEOR ABC");
        logs.Should().NotContain("Sentença publicada");
    }
}
