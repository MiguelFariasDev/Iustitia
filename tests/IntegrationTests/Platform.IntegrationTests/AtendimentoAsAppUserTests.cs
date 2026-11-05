using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// O mesmo fluxo do handoff, mas com a API conectada como <c>app_user</c> — a identidade de
/// produção. É o que prova que o RLS e os grants por coluna de 014_atendimentos.sql NÃO
/// quebram a aplicação: os demais testes HTTP rodam como dono das tabelas, que ignora ambos, e
/// um UPDATE em coluna sem permissão só apareceria em produção.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class AtendimentoAsAppUserTests(PlatformApiFactory factory) : IDisposable
{
    private readonly FakeBotGateway _gateway = new();

    private WebApplicationFactory<Program>? _host;

    private WebApplicationFactory<Program> Host => _host ??= BuildHost();

    /// <summary>
    /// Outros hosts desta collection rodam como dono das tabelas e criam o schema do Hangfire; em
    /// produção só existe um role, então isto é um artefato do harness: libera o schema para o
    /// <c>app_user</c> antes de subir o host dele.
    /// </summary>
    private WebApplicationFactory<Program> BuildHost()
    {
        using (var connection = new NpgsqlConnection(factory.SuperuserConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'hangfire') THEN
                    GRANT ALL ON SCHEMA hangfire TO app_user;
                    GRANT ALL ON ALL TABLES IN SCHEMA hangfire TO app_user;
                    GRANT ALL ON ALL SEQUENCES IN SCHEMA hangfire TO app_user;
                  END IF;
                END
                $$;
                """;
            command.ExecuteNonQuery();
        }

        return factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", factory.AppUserConnectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBotGateway>();
            services.AddSingleton<IBotGateway>(_gateway);
        });
    });
    }

    public void Dispose() => _host?.Dispose();

    private HttpClient Lawyer(Guid tenantId, string role = "Lawyer")
    {
        var client = Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "adv@teste.com", tenantId, role));
        return client;
    }

    [Fact]
    public async Task TheWholeHandoff_WorksUnderRlsAndMinimalGrants()
    {
        var tenant = Guid.NewGuid();
        var phone = "5585" + Random.Shared.NextInt64(900_000_000, 999_999_999);

        // Chave do Bot: a busca pelo hash acontece SEM tenant na sessão (bot_api_keys não tem RLS).
        var issued = await (await Lawyer(tenant, "Owner").PostAsJsonAsync("/api/v1/bot/api-keys", new { name = "Bot" }))
            .Content.ReadAsWebJsonAsync<IssuedBotApiKeyResponse>();
        var bot = Host.CreateClient();
        bot.DefaultRequestHeaders.Add("X-Api-Key", issued!.ApiKey);
        var lawyer = Lawyer(tenant);

        var started = await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = phone, contactName = "Maria" });
        started.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await started.Content.ReadAsWebJsonAsync<StartAtendimentoResponse>())!.AtendimentoId;

        (await bot.PostAsJsonAsync($"/api/v1/bot/atendimentos/{id}/mensagens",
                new { direction = "Inbound", sender = "Client", body = "Oi", externalId = "wamid.rls.1" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Renomear o contato exercita UPDATE em contact_name.
        (await bot.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = phone, contactName = "Maria S." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await bot.PostAsync($"/api/v1/bot/atendimentos/{id}/handoff", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Human" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Envio: INSERT da mensagem Pending e, depois, UPDATE de delivery/external_id.
        var sent = await lawyer.PostAsJsonAsync($"/api/v1/atendimentos/{id}/mensagens", new { body = "Bom dia" });
        sent.StatusCode.Should().Be(HttpStatusCode.OK);

        (await lawyer.PutAsJsonAsync($"/api/v1/atendimentos/{id}/cliente", new { clienteId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Bot" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await lawyer.PatchAsJsonAsync($"/api/v1/atendimentos/{id}/status", new { status = "Closed" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var history = await (await lawyer.GetAsync($"/api/v1/atendimentos/{id}/mensagens")).Content.ReadAsWebJsonAsync<ListMensagensResponse>();
        history!.Items.Select(m => m.Body).Should().Equal("Oi", "Bom dia");
        history.Items.Last().Delivery.Should().Be("Sent");
        _gateway.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task UnderRls_AnotherOfficeCannotSeeTheConversation_EvenKnowingItsId()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var keyA = (await (await Lawyer(tenantA, "Owner").PostAsJsonAsync("/api/v1/bot/api-keys", new { name = "A" }))
            .Content.ReadAsWebJsonAsync<IssuedBotApiKeyResponse>())!.ApiKey;
        var botA = Host.CreateClient();
        botA.DefaultRequestHeaders.Add("X-Api-Key", keyA);
        var started = await botA.PostAsJsonAsync("/api/v1/bot/atendimentos", new { phoneNumber = "5585988887777" });
        var id = (await started.Content.ReadAsWebJsonAsync<StartAtendimentoResponse>())!.AtendimentoId;

        (await Lawyer(tenantB).GetAsync($"/api/v1/atendimentos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Lawyer(tenantA).GetAsync($"/api/v1/atendimentos/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
