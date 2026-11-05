using System.Net;
using System.Text.Json;
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
/// outro. Ver ADR-039/067 e docs/api/conventions.md.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Login_AfterFiveAttemptsFromSameIp_ReturnsTooManyRequestsProblemDetails()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsync("/api/v1/auth/login", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var blocked = await client.PostAsync("/api/v1/auth/login", null);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        blocked.Headers.RetryAfter.Should().NotBeNull();

        using var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("COMMON_RATE_LIMITED");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
    }

    [Fact]
    public async Task Login_TrailingSlashAndCaseVariants_ShareTheSameBucket()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsync(i % 2 == 0 ? "/api/v1/auth/login/" : "/API/V1/AUTH/LOGIN", null);
        }

        (await client.PostAsync("/api/v1/auth/login", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_DifferentIps_HaveIndependentBuckets()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 6; i++)
        {
            await SendFromAsync(client, "203.0.113.10", "/api/v1/auth/login");
        }

        (await SendFromAsync(client, "203.0.113.10", "/api/v1/auth/login")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await SendFromAsync(client, "203.0.113.11", "/api/v1/auth/login")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Ipv6AddressesInTheSameSlash64_ShareTheSameBucket()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 5; i++)
        {
            await SendFromAsync(client, $"2001:db8:abcd:1::{i + 1}", "/api/v1/auth/login");
        }

        (await SendFromAsync(client, "2001:db8:abcd:1:ffff::9", "/api/v1/auth/login"))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ForgotPassword_AfterThreeAttempts_ReturnsTooManyRequests()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsync("/api/v1/auth/forgot-password", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.PostAsync("/api/v1/auth/forgot-password", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_ExhaustingItsBucket_DoesNotAffectOtherRoutes()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 6; i++)
        {
            await client.PostAsync("/api/v1/auth/login", null);
        }

        (await client.GetAsync("/api/v1/other")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Writes_AreLimitedPerUser_NotPerIp()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        // Dois advogados atrás do mesmo IP (NAT do escritório): cada um tem o seu balde de 60.
        for (var i = 0; i < 60; i++)
        {
            await SendFromAsync(client, "198.51.100.5", "/api/v1/things", HttpMethod.Post, user: "user-a");
        }

        (await SendFromAsync(client, "198.51.100.5", "/api/v1/things", HttpMethod.Post, user: "user-a"))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await SendFromAsync(client, "198.51.100.5", "/api/v1/things", HttpMethod.Post, user: "user-b"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reads_AllowThreeHundredPerMinute_ThenBlock()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 300; i++)
        {
            (await SendFromAsync(client, "198.51.100.7", "/api/v1/things", HttpMethod.Get, user: "user-a"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await SendFromAsync(client, "198.51.100.7", "/api/v1/things", HttpMethod.Get, user: "user-a"))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login", RateLimitCategory.Login)]
    [InlineData("POST", "/api/v1/auth/login/", RateLimitCategory.Login)]
    [InlineData("POST", "/api/v1/auth/forgot-password", RateLimitCategory.ForgotPassword)]
    [InlineData("POST", "/api/v1/auth/refresh", RateLimitCategory.Auth)]
    [InlineData("POST", "/api/v1/webhooks/whatsapp", RateLimitCategory.Webhook)]
    [InlineData("GET", "/api/v1/webhooks/whatsapp", RateLimitCategory.Webhook)]
    [InlineData("POST", "/api/v1/clientes", RateLimitCategory.Write)]
    [InlineData("PUT", "/api/v1/clientes/1", RateLimitCategory.Write)]
    [InlineData("DELETE", "/api/v1/clientes/1", RateLimitCategory.Write)]
    [InlineData("PATCH", "/api/v1/clientes/1", RateLimitCategory.Write)]
    [InlineData("GET", "/api/v1/clientes", RateLimitCategory.Read)]
    public void Classify_MapsRequestsToTheExpectedCategory(string method, string path, RateLimitCategory expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        RateLimitingConfiguration.Classify(context.Request).Should().Be(expected);
    }

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8:abcd:1:1:2:3:4", "2001:db8:abcd:1::/64")]
    public void ClientIpKey_NormalizesAddresses(string address, string expected)
    {
        RateLimitingConfiguration.ClientIpKey(IPAddress.Parse(address)).Should().Be(expected);
    }

    private static Task<HttpResponseMessage> SendFromAsync(
        HttpClient client, string ip, string path, HttpMethod? method = null, string? user = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        request.Headers.Add("X-Test-Remote-Ip", ip);
        if (user is not null)
        {
            request.Headers.Add("X-Test-User", user);
        }

        return client.SendAsync(request);
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
                    // Simula o que UseForwardedHeaders/UseAuthentication fazem no host real: IP
                    // do cliente e identidade (claim "sub") já resolvidos antes do rate limiter.
                    app.Use((context, next) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Test-Remote-Ip", out var ip))
                        {
                            context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                        }

                        if (context.Request.Headers.TryGetValue("X-Test-User", out var user))
                        {
                            context.User = new System.Security.Claims.ClaimsPrincipal(
                                new System.Security.Claims.ClaimsIdentity(
                                    [new System.Security.Claims.Claim("sub", user.ToString())], "test"));
                        }

                        return next(context);
                    });
                    app.UseRateLimiter();
                    app.Run(context => context.Response.WriteAsync("ok"));
                });
            });

        return await builder.StartAsync();
    }
}
