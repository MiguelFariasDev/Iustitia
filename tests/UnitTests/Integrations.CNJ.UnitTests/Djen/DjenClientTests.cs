using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Integrations.CNJ.Application.Models;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;

public class DjenClientTests
{
    private static readonly DateOnly StartDate = new(2026, 9, 25);
    private static readonly DateOnly EndDate = new(2026, 9, 28);

    private static readonly string SuccessJson = """
        {
          "status": "success",
          "message": "",
          "count": 2,
          "items": [
            {
              "id": 987654,
              "hash": "abc123",
              "data_disponibilizacao": "2026-09-26",
              "siglaTribunal": "TJSP",
              "tipoComunicacao": "Intimação",
              "nomeOrgao": "1ª Vara Cível",
              "nomeClasse": "Procedimento Comum",
              "texto": "<p>Fica a parte intimada a manifestar-se no prazo de 15 dias.</p>",
              "numero_processo": "12345674820268260001",
              "numeroprocessocommascara": "1234567-48.2026.8.26.0001",
              "meio": "D",
              "link": "https://comunica.pje.jus.br/987654",
              "tipoDocumento": "Intimação",
              "numeroComunicacao": 1
            },
            {
              "id": 987655,
              "data_disponibilizacao": "2026-09-27",
              "siglaTribunal": "TJSP",
              "tipoComunicacao": "Despacho",
              "texto": "Vista às partes.",
              "numeroprocessocommascara": "7654321-87.2025.8.19.0500"
            }
          ]
        }
        """;

    private static (DjenClient Client, StubHttpMessageHandler Handler) BuildClient(
        Action<StubHttpMessageHandler> configure, CnjOptions? options = null)
    {
        var handler = new StubHttpMessageHandler();
        configure(handler);

        var effectiveOptions = options ?? new CnjOptions();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(effectiveOptions.BaseUrl) };

        var client = new DjenClient(
            httpClient, Options.Create(effectiveOptions), NullLogger<DjenClient>.Instance);

        return (client, handler);
    }

    [Fact]
    public async Task GetPublicationsAsync_MapsTheDjenPayloadToTheApplicationContract()
    {
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);

        var first = result.Value.Items[0];
        first.ExternalId.Should().Be("987654");
        first.CnjNumber.Should().Be("1234567-48.2026.8.26.0001");
        first.Tribunal.Should().Be("TJSP");
        first.CommunicationType.Should().Be("Intimação");
        first.PublishedAt.Should().Be(new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero));

        // O cliente NÃO normaliza o texto: a normalização é regra de domínio (RawContent) e
        // manter o bruto aqui preserva o que o tribunal realmente publicou para auditoria.
        first.RawContent.Should().Contain("<p>");
    }

    [Fact]
    public async Task GetPublicationsAsync_PreservesTheOriginMetadataAsJson()
    {
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.Value.Items[0].MetadataJson.Should().NotBeNull();
        result.Value.Items[0].MetadataJson.Should().Contain("abc123").And.Contain("1ª Vara Cível");
    }

    [Fact]
    public async Task GetPublicationsAsync_BuildsTheDjenQueryString()
    {
        var (client, handler) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson));

        await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP", Page: 2, PageSize: 50), CancellationToken.None);

        var query = handler.ReceivedUris.Single().Query;
        query.Should().Contain("dataDisponibilizacaoInicio=2026-09-25");
        query.Should().Contain("dataDisponibilizacaoFim=2026-09-28");
        query.Should().Contain("siglaTribunal=TJSP");
        query.Should().Contain("pagina=2");
        query.Should().Contain("itensPorPagina=50");
    }

    [Fact]
    public async Task GetPublicationsAsync_WithoutTribunal_OmitsTheTribunalFilter()
    {
        var (client, handler) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson));

        await client.GetPublicationsAsync(new CnjPublicationQuery(StartDate, EndDate, Tribunal: null), CancellationToken.None);

        handler.ReceivedUris.Single().Query.Should().NotContain("siglaTribunal");
    }

    [Fact]
    public async Task GetPublicationsAsync_ClampsThePageSizeToTheApiMaximum()
    {
        var (client, handler) = BuildClient(
            handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson),
            new CnjOptions { MaxPageSize = 100 });

        await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP", PageSize: 5000), CancellationToken.None);

        handler.ReceivedUris.Single().Query.Should().Contain("itensPorPagina=100");
    }

    [Fact]
    public async Task GetPublicationsAsync_DiscardsItemsMissingRequiredFields_WithoutFailingThePage()
    {
        // Um item malformado no meio da página não deve custar as outras centenas.
        const string json = """
            {
              "count": 3,
              "items": [
                { "id": 1, "data_disponibilizacao": "2026-09-26", "texto": "ok", "numeroprocessocommascara": "1234567-48.2026.8.26.0001" },
                { "id": 0, "data_disponibilizacao": "2026-09-26", "texto": "sem id", "numeroprocessocommascara": "1234567-48.2026.8.26.0001" },
                { "id": 3, "data_disponibilizacao": "2026-09-26", "texto": "sem processo" }
              ]
            }
            """;
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, json));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.ExternalId.Should().Be("1");
    }

    [Fact]
    public async Task GetPublicationsAsync_FallsBackToTheUnmaskedProcessNumber()
    {
        const string json = """
            { "count": 1, "items": [ { "id": 1, "data_disponibilizacao": "2026-09-26", "texto": "ok", "numero_processo": "12345674820268260001" } ] }
            """;
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, json));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.Value.Items.Single().CnjNumber.Should().Be("12345674820268260001");
    }

    [Fact]
    public async Task GetPublicationsAsync_WithEmptyBody_ReturnsAnEmptyPage()
    {
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, string.Empty));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPublicationsAsync_WithMalformedJson_ReturnsInvalidResponse()
    {
        var (client, _) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, "{ isto não é json"));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_RESPONSE));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, nameof(ErrorCode.CNJ_RATE_LIMITED))]
    [InlineData(HttpStatusCode.RequestTimeout, nameof(ErrorCode.CNJ_TIMEOUT))]
    [InlineData(HttpStatusCode.GatewayTimeout, nameof(ErrorCode.CNJ_TIMEOUT))]
    [InlineData(HttpStatusCode.Unauthorized, nameof(ErrorCode.CNJ_AUTH_FAILED))]
    [InlineData(HttpStatusCode.Forbidden, nameof(ErrorCode.CNJ_AUTH_FAILED))]
    [InlineData(HttpStatusCode.BadRequest, nameof(ErrorCode.CNJ_INVALID_REQUEST))]
    [InlineData(HttpStatusCode.ServiceUnavailable, nameof(ErrorCode.CNJ_UNAVAILABLE))]
    [InlineData(HttpStatusCode.InternalServerError, nameof(ErrorCode.CNJ_UNAVAILABLE))]
    public async Task GetPublicationsAsync_MapsHttpStatusToACatalogErrorCode(
        HttpStatusCode statusCode, string expectedErrorCode)
    {
        var (client, _) = BuildClient(handler => handler.RespondWithStatus(statusCode));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Fact]
    public async Task GetPublicationsAsync_WhenTheCnjIsUnreachable_ReturnsUnavailable()
    {
        var (client, _) = BuildClient(handler => handler.Throw(new HttpRequestException("conexão recusada")));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_UNAVAILABLE));
    }

    [Fact]
    public async Task GetPublicationsAsync_OnTimeout_ReturnsTimeout()
    {
        // TaskCanceledException sem cancelamento do chamador é como o HttpClient/Polly
        // sinalizam timeout.
        var (client, _) = BuildClient(handler => handler.Throw(new TaskCanceledException()));

        var result = await client.GetPublicationsAsync(
            new CnjPublicationQuery(StartDate, EndDate, "TJSP"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TIMEOUT));
    }

    [Fact]
    public async Task GetPublicationsAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        var (client, _) = BuildClient(handler => handler.Throw(new TaskCanceledException()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => client.GetPublicationsAsync(new CnjPublicationQuery(StartDate, EndDate, "TJSP"), cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task PingAsync_WhenTheCnjResponds_Succeeds()
    {
        var (client, handler) = BuildClient(handler => handler.RespondWithJson(HttpStatusCode.OK, SuccessJson));

        var result = await client.PingAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Consulta mínima: um único item, para não puxar uma página inteira só para um health check.
        handler.ReceivedUris.Single().Query.Should().Contain("itensPorPagina=1");
    }

    [Fact]
    public async Task PingAsync_WhenTheCnjIsDown_Fails()
    {
        var (client, _) = BuildClient(handler => handler.RespondWithStatus(HttpStatusCode.ServiceUnavailable));

        var result = await client.PingAsync(CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_UNAVAILABLE));
    }
}
