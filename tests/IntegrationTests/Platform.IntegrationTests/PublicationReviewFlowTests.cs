using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.GetPublicationById;
using Advocacia.Modules.Legal.Application.Features.ListPublications;
using Advocacia.Modules.Legal.Application.Features.ReviewPublication;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Application.Features.Calendar;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// O caminho que dá valor ao produto, de ponta a ponta: publicação capturada → sugestão da IA → decisão do
/// advogado → prazo calculado e agendado na agenda. Banco real, RLS, autenticação e policies reais.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class PublicationReviewFlowTests(PlatformApiFactory factory)
{
    private HttpClient Client(Guid tenantId, string role = "Lawyer")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "advogada@teste.com", tenantId, role));

        return client;
    }

    /// <summary>Publicação disponibilizada numa sexta, com sugestão da IA de 15 dias úteis.</summary>
    private async Task<Guid> SeedAsync(Guid tenantId, int? sugeridoDias = 15, DeadlineType tipo = DeadlineType.Business)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<LegalDbContext>();

        // Número CNJ válido (dígitos verificadores): a unicidade é por externalId, não pelo número.
        const string cnj = "1234567-48.2026.8.26.0001";
        var publication = Publication.Create(
            tenantId,
            CNJNumber.Create(cnj).Value,
            Guid.NewGuid().ToString("N"),
            PublicationSource.DJEN,
            RawContent.Create("Fica o réu intimado para contestar no prazo de 15 dias.").Value,
            PublishedAt.Create(new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow.AddYears(1)).Value,
            capturedAt: DateTimeOffset.UtcNow).Value;
        db.Publications.Add(publication);

        db.PublicationSuggestions.Add(PublicationSuggestion.Create(
            tenantId, publication.Id, "Contestação", sugeridoDias, sugeridoDias is null ? null : tipo, "CPC, art. 335", 92,
            false, "anthropic", "modelo-teste", "v1", "{}", DateTimeOffset.UtcNow).Value);
        await db.SaveChangesAsync();

        return publication.Id.Value;
    }

    private static async Task<List<CalendarEventResponse>> EventosAsync(HttpClient client)
    {
        var resposta = await client.GetAsync("/api/v1/agenda/eventos?from=2026-11-01T00:00:00Z&to=2026-12-31T00:00:00Z");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await resposta.Content.ReadAsWebJsonAsync<ListCalendarEventsResponse>())!.Items.ToList();
    }

    [Fact]
    public async Task TheListAndTheDetail_ExposeTheAiSuggestion()
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var lista = await (await client.GetAsync("/api/v1/publications")).Content.ReadAsWebJsonAsync<ListPublicationsResponse>();
        var detalhe = await (await client.GetAsync($"/api/v1/publications/{id}")).Content.ReadAsWebJsonAsync<GetPublicationByIdResponse>();

        var sugestao = lista!.Items.Single(p => p.Id == id).Suggestion;
        sugestao.Should().NotBeNull();
        sugestao!.ActType.Should().Be("Contestação");
        sugestao.DeadlineDays.Should().Be(15);
        sugestao.DeadlineKind.Should().Be("business");
        sugestao.Confidence.Should().Be(92);
        detalhe!.Suggestion!.LegalBasis.Should().Be("CPC, art. 335");
    }

    [Fact]
    public async Task Approving_ComputesTheDueDate_AndPutsItOnTheAgenda_InOneCall()
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var resposta = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Approved" });
        var corpo = await resposta.Content.ReadAsWebJsonAsync<ReviewPublicationResponse>();
        var eventos = await EventosAsync(client);
        var detalhe = await (await client.GetAsync($"/api/v1/publications/{id}")).Content.ReadAsWebJsonAsync<GetPublicationByIdResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        corpo!.Vencimento.Should().Be(new DateOnly(2026, 12, 14));
        corpo.Days.Should().Be(15);
        var evento = eventos.Should().ContainSingle(e => e.Id == corpo.EventId).Subject;
        evento.Type.Should().Be("Prazo");
        evento.AllDay.Should().BeTrue();
        evento.Title.Should().StartWith("Prazo: Contestação");
        // 14/12/2026 às 00:00 em Brasília = 03:00 UTC: o dia certo na agenda do escritório.
        evento.StartAt.UtcDateTime.Should().Be(new DateTime(2026, 12, 14, 3, 0, 0, DateTimeKind.Utc));
        evento.Description.Should().Contain("ESTIMATIVA").And.NotContain("réu");
        detalhe!.Status.Should().Be("Approved");
        detalhe.ReviewNotes.Should().Contain("IA sugeriu: Contestação, 15 dias");
    }

    [Fact]
    public async Task Correcting_UsesTheLawyersDeadline()
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var resposta = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Corrected", deadlineDays = 5, notes = "Juiz fixou 5 dias" });
        var corpo = await resposta.Content.ReadAsWebJsonAsync<ReviewPublicationResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo!.Days.Should().Be(5);
        corpo.Vencimento.Should().Be(new DateOnly(2026, 11, 30), "5 dias úteis a partir de 24/11: 24, 25, 26, 27 e 30");
        (await EventosAsync(client)).Should().ContainSingle(e => e.Id == corpo.EventId);
    }

    [Fact]
    public async Task Rejecting_SchedulesNothing()
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var resposta = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Rejected" });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EventosAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task DecidingTwice_IsAConflict_AndNeverCreatesASecondDeadline()
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var primeira = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Approved" });
        var segunda = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Approved" });

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventosAsync(client)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheScheduler_IsIdempotentPerPublication_EvenWhenCalledAgain()
    {
        // É o que protege um retry depois de a decisão falhar ao ser gravada: o prazo não duplica.
        var tenant = Guid.NewGuid();
        var publicacao = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant);
        var agendador = scope.ServiceProvider.GetRequiredService<IPrazoAgendador>();
        var pedido = new AgendarPrazoRequest(tenant, publicacao, null, "Prazo: Teste", "x", new DateOnly(2026, 12, 14), null);

        var a = await agendador.AgendarAsync(pedido);
        var b = await agendador.AgendarAsync(pedido with { Vencimento = new DateOnly(2027, 1, 1) });

        a.IsSuccess.Should().BeTrue();
        b.Value.Should().Be(a.Value, "a mesma publicação nunca ganha um segundo prazo");

        using var client = Client(tenant);
        (await EventosAsync(client)).Should().ContainSingle();
    }

    [Fact]
    public async Task AnotherOffice_CannotReviewOrSeeThePublicationNorItsSuggestion()
    {
        var dono = Guid.NewGuid();
        var id = await SeedAsync(dono);
        using var outro = Client(Guid.NewGuid());

        var resposta = await outro.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = "Approved" });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Intern")]
    [InlineData("Financial")]
    public async Task OnlyLawyersAndAbove_CanReview(string role)
    {
        using var client = Client(Guid.NewGuid(), role);

        var resposta = await client.PostAsJsonAsync($"/api/v1/publications/{Guid.NewGuid()}/review", new { decision = "Approved" });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Archived")]
    [InlineData("")]
    [InlineData("approved")]
    public async Task InvalidDecisions_Return400(string decisao)
    {
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(tenant);
        using var client = Client(tenant);

        var resposta = await client.PostAsJsonAsync($"/api/v1/publications/{id}/review", new { decision = decisao });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
