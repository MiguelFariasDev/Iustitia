using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Ver ADR-037 e docs/observability/logging.md.</summary>
[Collection(PlatformApiCollection.Name)]
public class CorrelationIdTests(PlatformApiFactory factory)
{
    private const string HeaderName = "X-Correlation-Id";

    private HttpClient CreateClient() => factory.CreateClient();

    [Fact]
    public async Task Request_WithoutCorrelationIdHeader_GetsOneGeneratedInResponse()
    {
        var response = await CreateClient().GetAsync("/health/live");

        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();
        Guid.TryParse(values!.Single(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Request_WithCorrelationIdHeader_EchoesTheSameValueBack()
    {
        var correlationId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(HeaderName, correlationId.ToString());

        var response = await CreateClient().SendAsync(request);

        response.Headers.GetValues(HeaderName).Single().Should().Be(correlationId.ToString());
    }
}
