using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.GetPublicationById;
using Advocacia.Modules.Legal.Application.Features.GetPublicationsSummary;
using Advocacia.Modules.Legal.Application.Features.ListPublications;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Endpoints de publicações (Legal.Api, Etapa 1.4) exercitados através do host real — mesmo
/// pipeline de middlewares, mesma validação de JWT, mesmas policies.
///
/// Vivem neste projeto, e não em Legal.IntegrationTests, porque é aqui que está o
/// <see cref="PlatformApiFactory"/>, que sobe o host completo (Hosts/Api/Program.cs) com o
/// JWT assinado por chave local no lugar do JWKS do Supabase. Duplicar esse factory só para
/// respeitar a fronteira de pastas custaria mais do que ganharia.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class PublicationsEndpointsTests(PlatformApiFactory factory)
{
    private const string BaseRoute = "/api/v1/publications";

    private HttpClient CreateAuthenticatedClient(Guid tenantId, string role = "Lawyer")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "advogada@teste.com", tenantId, role));

        return client;
    }

    /// <summary>
    /// Semeia direto pelo LegalDbContext do host: as publicações são capturadas por job, não
    /// criadas por API (não existe POST /publications de propósito — ver PublicationsEndpoints).
    /// </summary>
    private async Task<Publication> SeedPublicationAsync(Guid tenantId, string externalId, string cnjNumber = "1234567-48.2026.8.26.0001")
    {
        using var scope = factory.Services.CreateScope();
        // O container registra a INTERFACE (ITenantContext -> AsyncLocalTenantContext); pedir
        // o tipo concreto falha. O tenant precisa estar definido antes de o LegalDbContext ser
        // materializado: é dele que saem o filtro global e o SET app.current_tenant do RLS.
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);

        var dbContext = scope.ServiceProvider.GetRequiredService<LegalDbContext>();

        var publication = Publication.Create(
            tenantId,
            CNJNumber.Create(cnjNumber).Value,
            externalId,
            PublicationSource.DJEN,
            RawContent.Create("Fica a parte intimada a manifestar-se no prazo de 15 dias.").Value,
            PublishedAt.Create(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow).Value,
            capturedAt: DateTimeOffset.UtcNow).Value;

        dbContext.Publications.Add(publication);
        await dbContext.SaveChangesAsync();

        return publication;
    }

    [Theory]
    [InlineData(BaseRoute)]
    [InlineData($"{BaseRoute}/summary")]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized(string route)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_WithoutAnyPublication_ReturnsAnEmptyPage()
    {
        // Escritório recém-criado: a tela precisa de um estado vazio, não de um 404.
        var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync(BaseRoute);
        var page = await response.Content.ReadAsWebJsonAsync<ListPublicationsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task List_ReturnsOnlyAContentPreview_NeverTheFullText()
    {
        // Uma listagem com o inteiro teor trafegaria centenas de KB de dado sigiloso sem
        // necessidade (ver docs/modules/legal/publications.md).
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        var page = await (await client.GetAsync(BaseRoute)).Content.ReadAsWebJsonAsync<ListPublicationsResponse>();

        var item = page!.Items.Should().ContainSingle().Subject;
        item.ContentPreview.Should().NotBeNullOrWhiteSpace();
        item.GetType().GetProperty("RawContent").Should().BeNull("o item de listagem não deve expor o inteiro teor");
    }

    [Fact]
    public async Task List_NeverLeaksAnotherTenantsPublications()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedPublicationAsync(tenantA, $"ext-a-{Guid.NewGuid():N}");
        await SeedPublicationAsync(tenantB, $"ext-b-{Guid.NewGuid():N}");

        var page = await (await CreateAuthenticatedClient(tenantA).GetAsync(BaseRoute))
            .Content.ReadAsWebJsonAsync<ListPublicationsResponse>();

        page!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task List_WithAnInvalidStatus_ReturnsBadRequest()
    {
        var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync($"{BaseRoute}?status=nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ReturnsTheFullContent()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        var response = await client.GetAsync($"{BaseRoute}/{publication.Id.Value}");
        var detail = await response.Content.ReadAsWebJsonAsync<GetPublicationByIdResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.RawContent.Should().Contain("manifestar-se");
        detail.Status.Should().Be(nameof(PublicationStatus.PendingReview));
    }

    [Fact]
    public async Task GetById_FromAnotherTenant_ReturnsNotFound()
    {
        // 404 e não 403: confirmar que o recurso existe já seria vazamento.
        var owner = Guid.NewGuid();
        var publication = await SeedPublicationAsync(owner, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync($"{BaseRoute}/{publication.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAsRead_MarksTheReadFlag_WithoutTouchingTheReviewStatus()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        var response = await client.PatchAsync($"{BaseRoute}/{publication.Id.Value}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await client.GetAsync($"{BaseRoute}/{publication.Id.Value}"))
            .Content.ReadAsWebJsonAsync<GetPublicationByIdResponse>();

        detail!.IsRead.Should().BeTrue();

        // Ler não é revisar (ver ADR-012): a revisão humana é uma ação separada e explícita.
        detail.Status.Should().Be(nameof(PublicationStatus.PendingReview));
    }

    [Fact]
    public async Task MarkAsRead_Twice_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        await client.PatchAsync($"{BaseRoute}/{publication.Id.Value}/read", content: null);
        var response = await client.PatchAsync($"{BaseRoute}/{publication.Id.Value}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Summary_CountsOnlyTheCallersTenant()
    {
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}", "7654321-87.2025.8.19.0500");
        await SeedPublicationAsync(Guid.NewGuid(), $"ext-outro-{Guid.NewGuid():N}");

        var client = CreateAuthenticatedClient(tenantId);
        var summary = await (await client.GetAsync($"{BaseRoute}/summary"))
            .Content.ReadAsWebJsonAsync<GetPublicationsSummaryResponse>();

        summary!.Total.Should().Be(2);
        summary.PendingReview.Should().Be(2);
        summary.Unread.Should().Be(2);
    }

    [Fact]
    public async Task ChangeStatus_ApprovesAPendingPublication_AndTheListFiltersByIt()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        var response = await client.PatchAsJsonAsync(
            $"{BaseRoute}/{publication.Id.Value}/status", new { status = "Approved", notes = "Conferido" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approved = await (await client.GetAsync($"{BaseRoute}?status=Approved,Corrected"))
            .Content.ReadAsWebJsonAsync<ListPublicationsResponse>();
        approved!.Items.Should().ContainSingle().Which.Status.Should().Be("Approved");

        var pending = await (await client.GetAsync($"{BaseRoute}?status=PendingReview"))
            .Content.ReadAsWebJsonAsync<ListPublicationsResponse>();
        pending!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeStatus_BackToPendingReview_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);
        await client.PatchAsJsonAsync($"{BaseRoute}/{publication.Id.Value}/status", new { status = "Approved" });

        var response = await client.PatchAsJsonAsync($"{BaseRoute}/{publication.Id.Value}/status", new { status = "PendingReview" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangeStatus_WithUnknownStatus_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");

        var response = await CreateAuthenticatedClient(tenantId)
            .PatchAsJsonAsync($"{BaseRoute}/{publication.Id.Value}/status", new { status = "Foo" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoints_ForARoleBelowLawyer_AreForbidden()
    {
        var response = await CreateAuthenticatedClient(Guid.NewGuid(), role: "Financial").GetAsync(BaseRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_FiltersByTribunalAndSearchTerm()
    {
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}", "1234567-48.2026.8.26.0001");
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}", "7654321-87.2025.8.19.0500");
        var client = CreateAuthenticatedClient(tenantId);

        var tjsp = await (await client.GetAsync($"{BaseRoute}?tribunal=TJSP")).Content.ReadAsWebJsonAsync<ListPublicationsResponse>();
        var busca = await (await client.GetAsync($"{BaseRoute}?busca=7654321")).Content.ReadAsWebJsonAsync<ListPublicationsResponse>();

        tjsp!.Items.Should().ContainSingle().Which.Tribunal.Should().Be("TJSP");
        busca!.Items.Should().ContainSingle().Which.CnjNumber.Should().Be("7654321-87.2025.8.19.0500");
    }

    [Fact]
    public async Task Summary_ExposesTodayAndWeekCounters()
    {
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");

        var summary = await (await CreateAuthenticatedClient(tenantId).GetAsync($"{BaseRoute}/summary"))
            .Content.ReadAsWebJsonAsync<GetPublicationsSummaryResponse>();

        summary!.PendingReview.Should().Be(1);
        summary.TreatedThisWeek.Should().Be(0);
    }

    [Fact]
    public async Task Summary_ReflectsMarkingAsRead()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, $"ext-{Guid.NewGuid():N}");
        var client = CreateAuthenticatedClient(tenantId);

        await client.PatchAsync($"{BaseRoute}/{publication.Id.Value}/read", content: null);

        var summary = await (await client.GetAsync($"{BaseRoute}/summary"))
            .Content.ReadAsWebJsonAsync<GetPublicationsSummaryResponse>();

        summary!.Total.Should().Be(1);
        summary.Unread.Should().Be(0);
        summary.PendingReview.Should().Be(1, "marcar como lida não conclui a revisão");
    }
}
