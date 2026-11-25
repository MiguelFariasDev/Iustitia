using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Acompanhamento;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Integrations;

/// <summary>
/// Contra o juscraper REAL (e, por ele, o portal do TJAL). Só roda com ADVOCACIA_TEST_JUSCRAPER_URL e
/// ADVOCACIA_TEST_JUSCRAPER_KEY definidos; sem eles passa sem fazer nada — a CI não depende de rede.
/// O processo é o exemplo público do contrato do juscraper.
/// </summary>
public sealed class JuscraperRealTests
{
    private const string ProcessoTjal = "0700482-85.2026.8.02.0349";
    private const string ProcessoTrf1 = "0000832-35.2018.4.01.3202";

    private static IHttpClientFactory? FabricaReal()
    {
        var url = Environment.GetEnvironmentVariable("ADVOCACIA_TEST_JUSCRAPER_URL");
        var chave = Environment.GetEnvironmentVariable("ADVOCACIA_TEST_JUSCRAPER_KEY");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        var fabrica = Substitute.For<IHttpClientFactory>();
        fabrica.CreateClient(Arg.Any<string>()).Returns(_ =>
        {
            var client = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.Add("X-Api-Key", chave);
            return client;
        });

        return fabrica;
    }

    [Fact]
    public async Task Consulta_TJAL_VemDoPortal_ETRF1_CaiNoDataJud()
    {
        if (FabricaReal() is not { } fabrica)
        {
            return;
        }

        var dataJud = Substitute.For<IConsultaProcessoTribunal>();
        dataJud.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ProcessoTribunal?>(null));
        var composto = new ConsultaProcessoTribunalComFallback(
            new JuscraperConsultaProcessoTribunal(fabrica, NullLogger<JuscraperConsultaProcessoTribunal>.Instance),
            dataJud,
            Options.Create(new JuscraperOptions { BaseUrl = "http://real/" }),
            new ConsultaTribunalTelemetry(),
            NullLogger<ConsultaProcessoTribunalComFallback>.Instance);

        var tjal = await composto.ConsultarAsync(CNJNumber.Create(ProcessoTjal).Value);
        var trf1 = await composto.ConsultarAsync(CNJNumber.Create(ProcessoTrf1).Value);

        tjal.Value!.Fonte.Should().Be(FonteConsulta.Juscraper);
        tjal.Value.Tribunal.Should().Be("TJAL");
        tjal.Value.Grau.Should().Be("G1");
        tjal.Value.Movimentos.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(10);
        trf1.IsSuccess.Should().BeTrue();
        await dataJud.Received(1).ConsultarAsync(Arg.Is<CNJNumber>(n => n.Tribunal == "TRF1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acompanhamento_TJAL_TrazIdsEstaveis()
    {
        if (FabricaReal() is not { } fabrica)
        {
            return;
        }

        var juscraper = new JuscraperAcompanhamento(fabrica, NullLogger<JuscraperAcompanhamento>.Instance);
        var numero = CNJNumber.Create(ProcessoTjal).Value;

        var (primeira, _) = await juscraper.ObterAsync(numero, null, CancellationToken.None);
        var (segunda, _) = await juscraper.ObterAsync(numero, new DateOnly(2026, 9, 1), CancellationToken.None);

        primeira.Value.Andamentos.Should().NotBeEmpty().And.OnlyContain(a => !string.IsNullOrEmpty(a.ExternalId));
        segunda.Value.Andamentos.Select(a => a.ExternalId).Should().BeSubsetOf(
            primeira.Value.Andamentos.Select(a => a.ExternalId), "o id é o mesmo com ou sem 'desde'");
    }
}
