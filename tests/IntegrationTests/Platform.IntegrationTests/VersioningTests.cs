using System.Net;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Ver ADR-039.</summary>
[Collection(PlatformApiCollection.Name)]
public class VersioningTests(PlatformApiFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient();

    [Fact]
    public async Task V1_HealthLive_ReturnsOk()
    {
        var response = await CreateClient().GetAsync("/api/v1/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task V1_ReportsSupportedApiVersionsHeader()
    {
        var response = await CreateClient().GetAsync("/api/v1/health/live");

        response.Headers.TryGetValues("api-supported-versions", out var versions).Should().BeTrue();
        versions!.Should().Contain(v => v.Contains('1'));
    }

    [Fact]
    public async Task V2_UnsupportedVersion_IsRejected()
    {
        // Só existe a v1 (ver ADR-039) — v2 não bate com nenhum MapToApiVersion registrado.
        // Asp.Versioning rejeita antes do endpoint rodar; o corpo/status exatos (400 vs 404)
        // são detalhe de implementação da biblioteca, o que importa é nunca executar o handler.
        var response = await CreateClient().GetAsync("/api/v2/health/live");

        response.IsSuccessStatusCode.Should().BeFalse();
    }
}
