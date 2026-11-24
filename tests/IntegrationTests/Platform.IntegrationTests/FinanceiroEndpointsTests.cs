using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Modules.Financeiro.Application.Features.Lancamentos;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Financeiro (Financeiro.Api) pelo host real: mesmo pipeline, mesmo JWT, mesmas policies.</summary>
[Collection(PlatformApiCollection.Name)]
public class FinanceiroEndpointsTests(PlatformApiFactory factory)
{
    private HttpClient Client(Guid tenantId, string role = "Partner")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "socio@teste.com", tenantId, role));

        return client;
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

    private static async Task<LancamentoResponse> CriarAsync(
        HttpClient client, string tipo, decimal valor, DateOnly vencimento, bool jaPago = false, string descricao = "Lançamento de teste")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/financeiro/lancamentos",
            new CreateLancamentoCommand(tipo, descricao, "Maria Souza", null, valor, vencimento, jaPago));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadAsWebJsonAsync<LancamentoResponse>())!;
    }

    [Theory]
    [InlineData("/api/v1/financeiro/lancamentos")]
    [InlineData("/api/v1/financeiro/resumo")]
    [InlineData("/api/v1/financeiro/fluxo-caixa")]
    public async Task WithoutAuthentication_Returns401(string route)
    {
        (await factory.CreateClient().GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Lawyer")]
    [InlineData("Intern")]
    [InlineData("Admin")]
    public async Task TheOfficesCashbox_IsForbiddenToLawyersInternsAndAdmins(string role)
    {
        using var client = Client(Guid.NewGuid(), role);

        (await client.GetAsync("/api/v1/financeiro/resumo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/financeiro/lancamentos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Partner")]
    [InlineData("Financial")]
    public async Task OwnersPartnersAndFinance_CanUseTheCashbox(string role)
    {
        using var client = Client(Guid.NewGuid(), role);

        (await client.GetAsync("/api/v1/financeiro/resumo")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OverdueEntries_AreReportedAsLate_AndFilterable()
    {
        using var client = Client(Guid.NewGuid());
        var atrasado = await CriarAsync(client, "ContaPagar", 300m, Hoje.AddDays(-3));
        await CriarAsync(client, "ContaPagar", 100m, Hoje.AddDays(5));

        var todos = await (await client.GetAsync("/api/v1/financeiro/lancamentos")).Content.ReadAsWebJsonAsync<ListLancamentosResponse>();
        var soAtrasados = await (await client.GetAsync("/api/v1/financeiro/lancamentos?status=Atrasado")).Content.ReadAsWebJsonAsync<ListLancamentosResponse>();

        atrasado.Status.Should().Be("Atrasado");
        todos!.TotalCount.Should().Be(2);
        todos.Items.Select(i => i.Vencimento).Should().BeInAscendingOrder();
        soAtrasados!.Items.Should().ContainSingle().Which.Id.Should().Be(atrasado.Id);
    }

    [Fact]
    public async Task Summary_IsAggregatedInTheDatabase_WithExactCents()
    {
        using var client = Client(Guid.NewGuid());
        await CriarAsync(client, "Honorario", 0.10m, Hoje.AddDays(10));
        await CriarAsync(client, "Honorario", 0.20m, Hoje.AddDays(10));
        await CriarAsync(client, "Honorario", 1000m, Hoje, jaPago: true);
        await CriarAsync(client, "ContaPagar", 250.50m, Hoje.AddDays(2));
        await CriarAsync(client, "ContaPagar", 100m, Hoje, jaPago: true);
        await CriarAsync(client, "ContaReceber", 75m, Hoje.AddDays(1));

        var resumo = await (await client.GetAsync("/api/v1/financeiro/resumo")).Content.ReadAsWebJsonAsync<ResumoFinanceiroResponse>();

        resumo!.HonorariosAReceber.Should().Be(0.30m, "0,10 + 0,20 não pode virar 0,30000000004");
        resumo.ContasAPagar.Should().Be(250.50m);
        resumo.ContasAReceber.Should().Be(75m);
        resumo.FaturamentoMes.Should().Be(1000m);
        resumo.Saldo.Should().Be(900m, "1000 recebidos − 100 pagos");
    }

    [Fact]
    public async Task Paying_MovesTheValueFromReceivableToBilling_AndCannotRepeat()
    {
        using var client = Client(Guid.NewGuid());
        var honorario = await CriarAsync(client, "Honorario", 800m, Hoje.AddDays(7));

        var pagar = await client.PostAsync($"/api/v1/financeiro/lancamentos/{honorario.Id}/pagar", null);
        var paga = await pagar.Content.ReadAsWebJsonAsync<LancamentoResponse>();
        var resumo = await (await client.GetAsync("/api/v1/financeiro/resumo")).Content.ReadAsWebJsonAsync<ResumoFinanceiroResponse>();
        var repetir = await client.PostAsync($"/api/v1/financeiro/lancamentos/{honorario.Id}/pagar", null);

        pagar.StatusCode.Should().Be(HttpStatusCode.OK);
        paga!.Status.Should().Be("Pago");
        paga.PagoEm.Should().NotBeNull();
        resumo!.HonorariosAReceber.Should().Be(0m);
        resumo.FaturamentoMes.Should().Be(800m);
        repetir.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CashFlow_CoversTheLastMonths_WithPaidEntriesOnly()
    {
        using var client = Client(Guid.NewGuid());
        await CriarAsync(client, "Honorario", 500m, Hoje, jaPago: true);
        await CriarAsync(client, "ContaPagar", 120m, Hoje, jaPago: true);
        await CriarAsync(client, "Honorario", 9999m, Hoje.AddDays(3));

        var fluxo = await (await client.GetAsync("/api/v1/financeiro/fluxo-caixa?meses=6")).Content.ReadAsWebJsonAsync<List<FluxoCaixaMesItem>>();

        fluxo.Should().HaveCount(6);
        fluxo![^1].Entradas.Should().Be(500m);
        fluxo[^1].Saidas.Should().Be(120m);
        fluxo.Take(5).Should().OnlyContain(m => m.Entradas == 0 && m.Saidas == 0);
    }

    [Fact]
    public async Task Delete_RemovesFromListAndTotals()
    {
        using var client = Client(Guid.NewGuid());
        var lancamento = await CriarAsync(client, "ContaPagar", 400m, Hoje.AddDays(4));

        var delete = await client.DeleteAsync($"/api/v1/financeiro/lancamentos/{lancamento.Id}");
        var lista = await (await client.GetAsync("/api/v1/financeiro/lancamentos")).Content.ReadAsWebJsonAsync<ListLancamentosResponse>();
        var resumo = await (await client.GetAsync("/api/v1/financeiro/resumo")).Content.ReadAsWebJsonAsync<ResumoFinanceiroResponse>();

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        lista!.Items.Should().BeEmpty();
        resumo!.ContasAPagar.Should().Be(0m);
    }

    [Fact]
    public async Task OneOffice_NeverSeesNorTouchesTheOthersEntries()
    {
        using var a = Client(Guid.NewGuid());
        using var b = Client(Guid.NewGuid());
        var doA = await CriarAsync(a, "Honorario", 5000m, Hoje.AddDays(1));

        var listaB = await (await b.GetAsync("/api/v1/financeiro/lancamentos")).Content.ReadAsWebJsonAsync<ListLancamentosResponse>();
        var resumoB = await (await b.GetAsync("/api/v1/financeiro/resumo")).Content.ReadAsWebJsonAsync<ResumoFinanceiroResponse>();
        var pagarPeloB = await b.PostAsync($"/api/v1/financeiro/lancamentos/{doA.Id}/pagar", null);
        var apagarPeloB = await b.DeleteAsync($"/api/v1/financeiro/lancamentos/{doA.Id}");

        listaB!.Items.Should().BeEmpty();
        resumoB!.HonorariosAReceber.Should().Be(0m);
        pagarPeloB.StatusCode.Should().Be(HttpStatusCode.NotFound);
        apagarPeloB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.999)]
    public async Task InvalidAmounts_Return400(double valor)
    {
        using var client = Client(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/v1/financeiro/lancamentos",
            new CreateLancamentoCommand("Honorario", "X", null, null, (decimal)valor, Hoje));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownTypeAndStatusFilter_Return400()
    {
        using var client = Client(Guid.NewGuid());

        (await client.PostAsJsonAsync("/api/v1/financeiro/lancamentos", new CreateLancamentoCommand("Outro", "X", null, null, 10m, Hoje)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/v1/financeiro/lancamentos?status=Inventado")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
