using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Atendimentos.Api;

public static class AtendimentoApiExtensions
{
    /// <summary>
    /// Registra o esquema de autenticação por chave de API do Bot e a policy que o exige.
    /// Chamar DEPOIS de <c>AddPlatformAuth</c>: o JWT do Supabase continua sendo o esquema
    /// padrão; o do Bot é opt-in, só nos endpoints que declaram a policy.
    /// </summary>
    public static IServiceCollection AddAtendimentoApi(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, BotApiKeyAuthenticationHandler>(BotAuthentication.Scheme, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(BotAuthentication.Policy, policy => policy
                .AddAuthenticationSchemes(BotAuthentication.Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim("tenant_id"));

        return services;
    }
}
