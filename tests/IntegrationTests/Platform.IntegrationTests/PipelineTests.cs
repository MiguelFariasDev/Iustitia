using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Advocacia.Platform.Application.Features.Settings.GetSettings;
using Advocacia.Platform.Application.Features.Settings.UpdateSettings;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Exercita o pipeline de CQRS de ponta a ponta contra o host real (ver ADR-030): Logging
/// → Validation → Performance → Transaction → Handler. Os behaviors já têm cobertura
/// unitária isolada (ver BuildingBlocks.Application.UnitTests/Behaviors) — aqui o que
/// importa é confirmar que estão de fato conectados via AddPlatformApplication, com
/// FluentValidation e MediatR resolvidos pelo container real, não por um mock.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class PipelineTests(PlatformApiFactory factory)
{
    [Fact]
    public async Task InvalidCommand_IsBlockedByValidationBehavior_BeforeReachingHandler()
    {
        using var client = factory.CreateClient();

        // Nome vazio viola CreateTenantValidator — nunca deveria chegar a
        // CreateTenantHandler (que chamaria o Supabase e criaria linhas no banco).
        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", new CreateTenantCommand("", TestCnpjGenerator.Generate(), "Owner", "owner@teste.com", "Senha123!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidCommand_CommitsThroughTransactionBehavior_AndIsVisibleAfterward()
    {
        using var client = factory.CreateClient();
        var tenantCommand = new CreateTenantCommand(
            "Escritorio Teste", TestCnpjGenerator.Generate(), "Owner Teste", $"owner-{Guid.NewGuid():N}@teste.com", "Senha123!");

        var created = await (await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", tenantCommand))
            .Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        var token = factory.JwtTokenFactory.CreateAccessToken(created!.AdminUserId, tenantCommand.AdminEmail, created.TenantId, "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Cria a configuração...
        var putResponse = await client.PutAsJsonAsync("/api/v1/settings", new UpdateSettingsCommand("dashboard.layout", "{\"cols\":2}"));
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // ...e confirma que a transação de fato commitou: uma NOVA requisição (nova conexão
        // do pool) enxerga o valor persistido, não apenas o estado em memória do handler.
        // Compara o JSON pelo valor, não pela string exata: a coluna é jsonb (ver
        // SettingConfiguration), e o Postgres reformata o texto ao reconstruí-lo (ex.:
        // adiciona espaço após ':'), então nunca preserva a formatação original enviada.
        var getResponse = await client.GetAsync("/api/v1/settings");
        var settings = await getResponse.Content.ReadAsWebJsonAsync<GetSettingsResponse>();

        var setting = settings!.Items.Should().ContainSingle(i => i.Key == "dashboard.layout").Subject;
        JsonDocument.Parse(setting.ValueJson).RootElement.GetProperty("cols").GetInt32().Should().Be(2);
    }
}
