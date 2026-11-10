using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Configuração <c>ForwardedHeaders</c>: como a API enxerga o cliente quando há um proxy reverso
/// na frente (Cloudflare Tunnel, nginx, balanceador). Sem isso, <c>RemoteIpAddress</c> é o IP do
/// proxy: o rate limit por IP junta todos os usuários numa partição só, o log mascara sempre o
/// mesmo IP e o <c>UseHttpsRedirection</c> enxerga HTTP.
/// <para>
/// Desligado por padrão: aceitar <c>X-Forwarded-*</c> de qualquer origem deixa o cliente forjar o
/// próprio IP e fugir do rate limit. Ligado, só se confia em quem está em
/// <see cref="KnownProxies"/>/<see cref="KnownNetworks"/> (loopback, se ambos vazios).
/// </para>
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    /// <summary>IPs de proxies confiáveis (o IP que a API vê na conexão, não o do cliente).</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Redes confiáveis em notação CIDR (ex.: a rede do Docker, "172.18.0.0/16").</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Quantos proxies confiáveis há em cadeia. 1 = só o cloudflared; 2 = cloudflared atrás de
    /// outro proxy. Valores maiores do que a cadeia real deixam o cliente forjar o IP.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    public const int MaxForwardLimit = 5;

    /// <summary>Falha na subida se a configuração for insegura ou ilegível.</summary>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (ForwardLimit is < 1 or > MaxForwardLimit)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ForwardLimit deve estar entre 1 e {MaxForwardLimit} (valor: {ForwardLimit}).");
        }

        _ = ParseProxies();
        _ = ParseNetworks();
    }

    internal void ApplyTo(ForwardedHeadersOptions options)
    {
        var proxies = ParseProxies();
        var networks = ParseNetworks();

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ForwardLimit;

        // Substitui os padrões do framework em vez de somar a eles: a lista confiável é sempre a
        // que está escrita na configuração.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        if (proxies.Count == 0 && networks.Count == 0)
        {
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
            return;
        }

        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    private List<IPAddress> ParseProxies()
    {
        var result = new List<IPAddress>();
        foreach (var raw in KnownProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            if (!IPAddress.TryParse(raw.Trim(), out var address))
            {
                throw new InvalidOperationException($"{SectionName}:KnownProxies contém um IP inválido: '{raw}'.");
            }

            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownProxies não pode ser '{raw}': confiar em qualquer origem anula a proteção contra IP forjado.");
            }

            result.Add(address);
        }

        return result;
    }

    private List<System.Net.IPNetwork> ParseNetworks()
    {
        var result = new List<System.Net.IPNetwork>();
        foreach (var raw in KnownNetworks.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (!System.Net.IPNetwork.TryParse(raw.Trim(), out var network))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownNetworks contém um CIDR inválido: '{raw}' (esperado algo como 172.18.0.0/16).");
            }

            if (network.PrefixLength == 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownNetworks não pode ser '{raw}': confiar em qualquer origem anula a proteção contra IP forjado.");
            }

            result.Add(network);
        }

        return result;
    }
}

public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddAdvocaciaForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        settings.Validate();

        services.AddSingleton(settings);
        if (settings.Enabled)
        {
            services.Configure<ForwardedHeadersOptions>(settings.ApplyTo);
        }

        return services;
    }

    /// <summary>
    /// Precisa ser o PRIMEIRO middleware: tudo que lê IP ou esquema (HTTPS redirection, CORS,
    /// rate limit, log) tem de ver o valor já corrigido. No-op se <c>ForwardedHeaders:Enabled</c>
    /// for falso.
    /// </summary>
    public static IApplicationBuilder UseAdvocaciaForwardedHeaders(this IApplicationBuilder app)
    {
        var settings = app.ApplicationServices.GetRequiredService<ForwardedHeadersSettings>();
        return settings.Enabled ? app.UseForwardedHeaders() : app;
    }
}
