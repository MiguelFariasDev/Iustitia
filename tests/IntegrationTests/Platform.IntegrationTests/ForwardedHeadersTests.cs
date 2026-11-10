using System.Net;
using Advocacia.Platform.Api.Extensions;
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
/// Sem banco nem RabbitMQ: monta só o pipeline mínimo (ForwardedHeaders + um endpoint que
/// devolve o que a API enxergou) com o <see cref="TestServer"/>. O que importa aqui é a
/// decisão de confiança — quem pode dizer qual é o IP do cliente.
/// </summary>
public sealed class ForwardedHeadersTests
{
    private static readonly IPAddress Cloudflared = IPAddress.Parse("172.18.0.5");

    private sealed record Seen(string? Ip, string Scheme);

    private static async Task<IHost> StartAsync(Dictionary<string, string?> config)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureAppConfiguration(c => c.AddInMemoryCollection(config))
                .ConfigureServices((ctx, services) => services.AddAdvocaciaForwardedHeaders(ctx.Configuration))
                .Configure(app => app.UseAdvocaciaForwardedHeaders()))
            .Build();
        await host.StartAsync();
        return host;
    }

    private static async Task<Seen> SendAsync(
        Dictionary<string, string?> config, IPAddress peer, string? forwardedFor, string? forwardedProto = "https")
    {
        Seen? seen = null;
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureAppConfiguration(c => c.AddInMemoryCollection(config))
                .ConfigureServices((ctx, services) => services.AddAdvocaciaForwardedHeaders(ctx.Configuration))
                .Configure(app =>
                {
                    app.UseAdvocaciaForwardedHeaders();
                    app.Run(context =>
                    {
                        seen = new Seen(context.Connection.RemoteIpAddress?.ToString(), context.Request.Scheme);
                        return Task.CompletedTask;
                    });
                }))
            .Build();
        await host.StartAsync();
        try
        {
            await host.GetTestServer().SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = peer;
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = "/";
                if (forwardedFor is not null)
                {
                    context.Request.Headers["X-Forwarded-For"] = forwardedFor;
                }

                if (forwardedProto is not null)
                {
                    context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
                }
            });
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }

        return seen!;
    }

    private static Dictionary<string, string?> Enabled(params (string Key, string Value)[] extra)
    {
        var config = new Dictionary<string, string?> { ["ForwardedHeaders:Enabled"] = "true" };
        foreach (var (key, value) in extra)
        {
            config[key] = value;
        }

        return config;
    }

    [Fact]
    public async Task Disabled_by_default_ignores_forwarded_headers()
    {
        var seen = await SendAsync(new Dictionary<string, string?>(), IPAddress.Loopback, "203.0.113.7");

        seen.Ip.Should().Be("127.0.0.1");
        seen.Scheme.Should().Be("http");
    }

    [Fact]
    public async Task Trusted_loopback_proxy_exposes_the_real_client_ip_and_scheme()
    {
        var seen = await SendAsync(Enabled(), IPAddress.Loopback, "203.0.113.7");

        seen.Ip.Should().Be("203.0.113.7");
        seen.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task Untrusted_peer_cannot_forge_ip_or_scheme()
    {
        // Alguém que alcança a API direto (sem passar pelo tunnel) e manda cabeçalhos falsos.
        var seen = await SendAsync(Enabled(), IPAddress.Parse("198.51.100.9"), "203.0.113.7");

        seen.Ip.Should().Be("198.51.100.9");
        seen.Scheme.Should().Be("http");
    }

    [Fact]
    public async Task Client_supplied_entries_are_ignored_when_forward_limit_is_one()
    {
        // O cliente manda "1.2.3.4"; a borda do Cloudflare acrescenta o IP que de fato viu.
        var seen = await SendAsync(Enabled(), IPAddress.Loopback, "1.2.3.4, 203.0.113.7");

        seen.Ip.Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task Trusts_a_configured_network_and_not_the_default_loopback()
    {
        var config = Enabled(("ForwardedHeaders:KnownNetworks:0", "172.18.0.0/16"));

        (await SendAsync(config, Cloudflared, "203.0.113.7")).Ip.Should().Be("203.0.113.7");
        (await SendAsync(config, IPAddress.Loopback, "203.0.113.7")).Ip.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Trusts_a_configured_proxy_address()
    {
        var config = Enabled(("ForwardedHeaders:KnownProxies:0", "172.18.0.5"));

        (await SendAsync(config, Cloudflared, "203.0.113.7")).Ip.Should().Be("203.0.113.7");
        (await SendAsync(config, IPAddress.Parse("172.18.0.6"), "203.0.113.7")).Ip.Should().Be("172.18.0.6");
    }

    [Fact]
    public async Task Chain_of_two_proxies_needs_forward_limit_two()
    {
        var config = Enabled(
            ("ForwardedHeaders:ForwardLimit", "2"),
            ("ForwardedHeaders:KnownProxies:0", "172.18.0.5"),
            ("ForwardedHeaders:KnownProxies:1", "10.0.0.2"));

        var seen = await SendAsync(config, Cloudflared, "203.0.113.7, 10.0.0.2");

        seen.Ip.Should().Be("203.0.113.7");
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "::/0")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "172.18.0.0")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "nao-e-cidr")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "0.0.0.0")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "::")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "cloudflared")]
    [InlineData("ForwardedHeaders:ForwardLimit", "0")]
    [InlineData("ForwardedHeaders:ForwardLimit", "6")]
    public async Task Unsafe_or_unreadable_configuration_fails_at_startup(string key, string value)
    {
        var act = () => StartAsync(Enabled((key, value)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ForwardedHeaders*");
    }

    [Fact]
    public async Task Invalid_values_are_not_checked_while_disabled()
    {
        var config = new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "0.0.0.0" };

        using var host = await StartAsync(config);

        host.Services.GetRequiredService<ForwardedHeadersSettings>().Enabled.Should().BeFalse();
    }
}
