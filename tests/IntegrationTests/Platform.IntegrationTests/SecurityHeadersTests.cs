using System.Net;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Headers de segurança (OWASP) e CORS estrito, em hosts mínimos sem Postgres — o que se
/// verifica aqui é a configuração de borda, não a lógica de negócio. Ver ADR-067.
/// </summary>
public class SecurityHeadersTests
{
    [Fact]
    public async Task EveryResponse_CarriesTheSecurityHeaders()
    {
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/v1/ok");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle("max-age=31536000; includeSubDomains");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("strict-origin-when-cross-origin");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("default-src 'none'");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
        response.Headers.GetValues("Cache-Control").Should().ContainSingle("no-store");
    }

    [Fact]
    public async Task XssProtectionHeader_IsExplicitlyDisabled_NotEnabledInBlockMode()
    {
        // OWASP: "1; mode=block" reintroduz vulnerabilidades nos navegadores que ainda têm o
        // filtro; o valor recomendado é "0". Ver comentário em SecurityHeadersMiddleware.
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/v1/ok");

        response.Headers.GetValues("X-XSS-Protection").Should().ContainSingle("0");
    }

    [Fact]
    public async Task InformationLeakingHeaders_AreRemoved()
    {
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/v1/leaky");

        response.Headers.Contains("Server").Should().BeFalse();
        response.Headers.Contains("X-Powered-By").Should().BeFalse();
    }

    [Fact]
    public async Task ErrorResponses_AlsoCarryTheHeaders()
    {
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/v1/boom");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task EndpointThatSetsCacheControl_IsNotOverridden()
    {
        using var host = await BuildHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/v1/cacheable");

        response.Headers.GetValues("Cache-Control").Should().ContainSingle("public, max-age=60");
    }

    [Fact]
    public async Task RelaxedPaths_GetTheLooserCsp_AndEverythingElseStaysStrict()
    {
        using var host = await BuildHostAsync(relaxedPrefixes: ["/swagger"]);
        var client = host.GetTestClient();

        var swagger = await client.GetAsync("/swagger/index.html");
        var api = await client.GetAsync("/api/v1/ok");

        swagger.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'self' 'unsafe-inline'");
        swagger.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
        api.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("default-src 'none'");
    }

    [Fact]
    public async Task Cors_AllowedOrigin_GetsAccessControlHeaders_WithoutCredentials()
    {
        using var host = await BuildHostAsync(corsOrigins: ["https://app.iustitia.com.br", "http://localhost:3000"]);

        var response = await host.GetTestClient().SendAsync(Preflight("https://app.iustitia.com.br", "POST", "authorization"));

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("https://app.iustitia.com.br");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain("POST").And.NotContain("TRACE");
    }

    [Fact]
    public async Task Cors_UnauthorizedOrigin_GetsNoAccessControlHeaders()
    {
        using var host = await BuildHostAsync(corsOrigins: ["https://app.iustitia.com.br"]);
        var client = host.GetTestClient();

        var preflight = await client.SendAsync(Preflight("https://evil.example", "POST", "authorization"));
        var simple = await SendWithOriginAsync(client, "https://evil.example");

        preflight.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        simple.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_OriginThatMerelyContainsAnAllowedOne_IsRejected()
    {
        using var host = await BuildHostAsync(corsOrigins: ["https://app.iustitia.com.br"]);

        var response = await SendWithOriginAsync(host.GetTestClient(), "https://app.iustitia.com.br.evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_DisallowedRequestHeader_IsNotAllowedInPreflight()
    {
        using var host = await BuildHostAsync(corsOrigins: ["https://app.iustitia.com.br"]);

        var response = await host.GetTestClient().SendAsync(
            Preflight("https://app.iustitia.com.br", "POST", "x-custom-evil"));

        response.Headers.GetValues("Access-Control-Allow-Headers").SelectMany(h => h.Split(','))
            .Should().NotContain(h => h.Trim().Equals("x-custom-evil", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cors_WithNoConfiguredOrigins_AllowsNothing()
    {
        using var host = await BuildHostAsync(corsOrigins: []);

        var response = await SendWithOriginAsync(host.GetTestClient(), "http://localhost:3000");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.iustitia.com.br")]
    [InlineData("http://app.iustitia.com.br")]
    [InlineData("https://app.iustitia.com.br/")]
    [InlineData("https://app.iustitia.com.br/path")]
    [InlineData("app.iustitia.com.br")]
    public void ValidateCorsOrigins_RejectsUnsafeOrMalformedOrigins(string origin)
    {
        var act = () => SecurityConfiguration.ValidateCorsOrigins([origin]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("https://app.iustitia.com.br")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    public void ValidateCorsOrigins_AcceptsHttpsOriginsAndLocalhost(string origin)
    {
        SecurityConfiguration.ValidateCorsOrigins([origin]).Should().ContainSingle(origin);
    }

    private static HttpRequestMessage Preflight(string origin, string method, string requestHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/ok");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", requestHeaders);
        return request;
    }

    private static Task<HttpResponseMessage> SendWithOriginAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ok");
        request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    private static async Task<IHost> BuildHostAsync(string[]? relaxedPrefixes = null, string[]? corsOrigins = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                (corsOrigins ?? []).Select((origin, index) => KeyValuePair.Create<string, string?>($"Cors:AllowedOrigins:{index}", origin)))
            .Build();

        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddAdvocaciaSecurityHeaders(relaxedPrefixes ?? []);
                    services.AddPlatformApi(configuration);
                });
                webHost.Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.UseCors(Advocacia.Platform.Api.Extensions.ServiceCollectionExtensions.CorsPolicyName);
                    app.Run(context =>
                    {
                        switch (context.Request.Path.Value)
                        {
                            case "/api/v1/leaky":
                                context.Response.Headers["Server"] = "Kestrel";
                                context.Response.Headers["X-Powered-By"] = "ASP.NET";
                                break;
                            case "/api/v1/boom":
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                break;
                            case "/api/v1/cacheable":
                                context.Response.Headers.CacheControl = "public, max-age=60";
                                break;
                        }

                        return context.Response.WriteAsync("ok");
                    });
                });
            });

        return await builder.StartAsync();
    }
}
