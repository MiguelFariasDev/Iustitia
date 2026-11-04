using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Integrations.CNJ.Application.Models;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;

/// <summary>Consulta dirigida por número de processo (Etapa 1.3).</summary>
public class DjenClientByProcessTests
{
    private const string EmptyPageJson = """{ "status": "success", "count": 0, "items": [] }""";

    private static (DjenClient Client, StubHttpMessageHandler Handler) BuildClient()
    {
        var handler = new StubHttpMessageHandler();
        handler.RespondWithJson(HttpStatusCode.OK, EmptyPageJson);

        var options = new CnjOptions();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };

        return (new DjenClient(httpClient, Options.Create(options), NullLogger<DjenClient>.Instance), handler);
    }

    [Fact]
    public async Task ByProcess_SendsNumeroProcessoWithoutMask()
    {
        // O DJEN só casa com o número sem máscara. Enviar mascarado devolve zero resultados
        // SEM erro — falha silenciosa, o pior tipo.
        var (client, handler) = BuildClient();

        await client.GetPublicationsAsync(
            CnjPublicationQuery.ByProcess("1234567-48.2026.8.26.0001"), CancellationToken.None);

        var query = handler.ReceivedUris.Single().Query;
        query.Should().Contain("numeroProcesso=12345674820268260001");
        query.Should().NotContain("1234567-48");
    }

    [Fact]
    public async Task ByProcess_AcceptsAnUnmaskedNumberUnchanged()
    {
        var (client, handler) = BuildClient();

        await client.GetPublicationsAsync(
            CnjPublicationQuery.ByProcess("12345674820268260001"), CancellationToken.None);

        handler.ReceivedUris.Single().Query.Should().Contain("numeroProcesso=12345674820268260001");
    }

    [Fact]
    public async Task ByProcess_WithoutDates_OmitsThePeriodFilter()
    {
        // Sem janela de datas, o DJEN devolve o histórico completo do processo — que é o que
        // um processo recém-cadastrado precisa.
        var (client, handler) = BuildClient();

        await client.GetPublicationsAsync(
            CnjPublicationQuery.ByProcess("12345674820268260001"), CancellationToken.None);

        var query = handler.ReceivedUris.Single().Query;
        query.Should().NotContain("dataDisponibilizacaoInicio");
        query.Should().NotContain("dataDisponibilizacaoFim");
        query.Should().NotContain("siglaTribunal");
    }

    [Fact]
    public async Task ByProcess_WithDates_NarrowsTheHistory()
    {
        var (client, handler) = BuildClient();

        await client.GetPublicationsAsync(
            CnjPublicationQuery.ByProcess(
                "12345674820268260001", new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 28)),
            CancellationToken.None);

        var query = handler.ReceivedUris.Single().Query;
        query.Should().Contain("numeroProcesso=12345674820268260001");
        query.Should().Contain("dataDisponibilizacaoInicio=2026-01-01");
        query.Should().Contain("dataDisponibilizacaoFim=2026-09-28");
    }

    [Fact]
    public async Task ByPeriod_StillSendsTheDateAndTribunalFilters()
    {
        var (client, handler) = BuildClient();

        await client.GetPublicationsAsync(
            CnjPublicationQuery.ByPeriod(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 28), "TJSP"),
            CancellationToken.None);

        var query = handler.ReceivedUris.Single().Query;
        query.Should().Contain("dataDisponibilizacaoInicio=2026-09-25");
        query.Should().Contain("siglaTribunal=TJSP");
        query.Should().NotContain("numeroProcesso");
    }

    [Fact]
    public async Task WithoutAnyCriteria_IsRejectedWithoutTouchingTheNetwork()
    {
        // Uma consulta sem período e sem processo puxaria o diário nacional inteiro.
        var (client, handler) = BuildClient();

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate: null, EndDate: null, Tribunal: "TJSP"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_REQUEST));
        handler.ReceivedUris.Should().BeEmpty();
    }

    [Theory]
    [InlineData("TJSP", null, "tribunal=TJSP")]
    [InlineData(null, "12345674820268260001", "processo=12345674820268260001")]
    public void Describe_IsLowCardinalityAndSaysWhichModeWasUsed(string? tribunal, string? cnjNumber, string expected)
    {
        var query = new CnjPublicationQuery(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 28), tribunal, cnjNumber);

        query.Describe().Should().Contain(expected);
    }

    [Fact]
    public void HasCriteria_IsFalseOnlyWhenNeitherPeriodNorProcessIsSet()
    {
        new CnjPublicationQuery(null, null, "TJSP").HasCriteria.Should().BeFalse();
        CnjPublicationQuery.ByProcess("12345674820268260001").HasCriteria.Should().BeTrue();
        CnjPublicationQuery.ByPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)).HasCriteria.Should().BeTrue();
    }
}
