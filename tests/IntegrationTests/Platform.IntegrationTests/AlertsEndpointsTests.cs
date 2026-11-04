using System.Net;
using System.Net.Http.Headers;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Features.Alerts;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Alertas in-app pelo host real. O alerta é pessoal: dois advogados do mesmo escritório
/// NÃO podem ver os alertas um do outro, e RLS por tenant sozinho não garante isso.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class AlertsEndpointsTests(PlatformApiFactory factory)
{
    private const string BaseRoute = "/api/v1/alerts";

    private HttpClient ClientFor(Guid tenantId, Guid userId, string role = "Lawyer")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(userId, "advogada@teste.com", tenantId, role));

        return client;
    }

    private async Task<bool> SeedAlertAsync(Guid tenantId, Guid userId, Guid? referenceId = null, string type = "Publication")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);

        return await scope.ServiceProvider.GetRequiredService<IAlertPublisher>().PublishAsync(
            new AlertRequest(tenantId, userId, type, "Nova publicação", "Processo 1234567-48.2026.8.26.0001", referenceId ?? Guid.NewGuid()));
    }

    [Fact]
    public async Task WithoutAuthentication_ReturnsUnauthorized()
    {
        (await factory.CreateClient().GetAsync(BaseRoute)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForARoleBelowLawyer_ReturnsForbidden()
    {
        var response = await ClientFor(Guid.NewGuid(), Guid.NewGuid(), "Financial").GetAsync(BaseRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCallersAlerts_NotAColleaguesInTheSameTenant()
    {
        var tenantId = Guid.NewGuid();
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await SeedAlertAsync(tenantId, ana);
        await SeedAlertAsync(tenantId, ana);
        await SeedAlertAsync(tenantId, bruno);

        var page = await (await ClientFor(tenantId, ana).GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        page!.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(item => item.Type == "Publication");
    }

    [Fact]
    public async Task List_NeverLeaksAnotherTenantsAlerts()
    {
        var userId = Guid.NewGuid();
        await SeedAlertAsync(Guid.NewGuid(), userId);

        var page = await (await ClientFor(Guid.NewGuid(), userId).GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        page!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_IsIdempotentPerReference()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reference = Guid.NewGuid();

        (await SeedAlertAsync(tenantId, userId, reference)).Should().BeTrue();
        (await SeedAlertAsync(tenantId, userId, reference)).Should().BeFalse();

        var page = await (await ClientFor(tenantId, userId).GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>();
        page!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetById_OfAColleaguesAlert_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var ana = Guid.NewGuid();
        await SeedAlertAsync(tenantId, ana);
        var anaAlert = (await (await ClientFor(tenantId, ana).GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>())!.Items.Single();

        var response = await ClientFor(tenantId, Guid.NewGuid()).GetAsync($"{BaseRoute}/{anaAlert.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAsRead_UpdatesSummary_AndASecondCallConflicts()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAlertAsync(tenantId, userId);
        await SeedAlertAsync(tenantId, userId);
        var client = ClientFor(tenantId, userId);
        var alert = (await (await client.GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>())!.Items.First();

        var first = await client.PatchAsync($"{BaseRoute}/{alert.Id}/read", content: null);
        var second = await client.PatchAsync($"{BaseRoute}/{alert.Id}/read", content: null);
        var summary = await (await client.GetAsync($"{BaseRoute}/summary")).Content.ReadAsWebJsonAsync<AlertsSummaryResponse>();

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        summary!.Total.Should().Be(2);
        summary.Unread.Should().Be(1);
        summary.UnreadByType["Publication"].Should().Be(1);
    }

    [Fact]
    public async Task ReadAll_MarksEverythingRead()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAlertAsync(tenantId, userId);
        await SeedAlertAsync(tenantId, userId);
        await SeedAlertAsync(tenantId, Guid.NewGuid()); // de outro usuário: não pode ser tocado
        var client = ClientFor(tenantId, userId);

        var response = await client.PatchAsync($"{BaseRoute}/read-all", content: null);
        var unread = await (await client.GetAsync($"{BaseRoute}?isRead=false")).Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsWebJsonAsync<int>()).Should().Be(2);
        unread!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Dismiss_RemovesTheAlertFromTheListing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAlertAsync(tenantId, userId);
        var client = ClientFor(tenantId, userId);
        var alert = (await (await client.GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>())!.Items.Single();

        var response = await client.DeleteAsync($"{BaseRoute}/{alert.Id}");
        var after = await (await client.GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Should().BeEmpty();
        (await client.DeleteAsync($"{BaseRoute}/{alert.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersBySeveralTypesAtOnce()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAlertAsync(tenantId, userId, type: "Publication");
        await SeedAlertAsync(tenantId, userId, type: "Task");
        await SeedAlertAsync(tenantId, userId, type: "Birthday");

        var others = await (await ClientFor(tenantId, userId).GetAsync($"{BaseRoute}?type=Task,Birthday,Other"))
            .Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        others!.Items.Select(i => i.Type).Should().BeEquivalentTo(["Task", "Birthday"]);
    }

    [Fact]
    public async Task List_FiltersByTypeAndRejectsAnUnknownOne()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAlertAsync(tenantId, userId, type: "Publication");
        await SeedAlertAsync(tenantId, userId, type: "Task");
        var client = ClientFor(tenantId, userId);

        var tasks = await (await client.GetAsync($"{BaseRoute}?type=Task")).Content.ReadAsWebJsonAsync<AlertsPageResponse>();

        tasks!.Items.Should().ContainSingle().Which.Type.Should().Be("Task");
        (await client.GetAsync($"{BaseRoute}?type=Nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
