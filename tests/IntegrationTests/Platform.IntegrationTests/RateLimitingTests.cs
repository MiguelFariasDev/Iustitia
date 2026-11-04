using System.Net;
using Advocacia.Platform.Api.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Testa <see cref="RateLimitingConfiguration"/> isoladamente, num host mínimo próprio (sem
/// Postgres/Testcontainers, sem compartilhar estado com <see cref="Common.PlatformApiFactory"/>)
/// — o rate limiter guarda contadores em memória por processo; reaproveitar o host
/// compartilhado dos outros testes de integração poderia fazer um teste "gastar" o limite de
/// outro. Ver ADR-039/docs/api/conventions.md.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task AuthEndpoint_AfterTenRequestsInAMinute_ReturnsTooManyRequests()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await client.PostAsync("/api/v1/auth/login", null);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task NonAuthEndpoint_IsNotAffectedByAuthPolicyLimit()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 11; i++)
        {
            await client.PostAsync("/api/v1/auth/login", null);
        }

        var response = await client.GetAsync("/api/v1/other");

        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    private static async Task<IHost> BuildHostAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddAdvocaciaRateLimiting());
                webHost.Configure(app =>
                {
                    app.UseRateLimiter();
                    app.Map("/api/v1/auth/login", inner => inner.Run(context => context.Response.WriteAsync("ok")));
                    app.Map("/api/v1/other", inner => inner.Run(context => context.Response.WriteAsync("ok")));
                });
            });

        return await builder.StartAsync();
    }
}
