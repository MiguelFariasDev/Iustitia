using System.Net;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Ver docs/observability/health-checks.md.</summary>
[Collection(PlatformApiCollection.Name)]
public class HealthChecksTests(PlatformApiFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient();

    [Fact]
    public async Task HealthLive_ReturnsOk_WithoutCheckingDependencies()
    {
        var response = await CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk_WhenDependenciesAreHealthy()
    {
        var response = await CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsStructuredJson_WithAllChecks()
    {
        var response = await CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"checks\"").And.Contain("postgres").And.Contain("redis");
    }

    [Fact]
    public async Task Metrics_RequiresAuthentication()
    {
        var response = await CreateClient().GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
