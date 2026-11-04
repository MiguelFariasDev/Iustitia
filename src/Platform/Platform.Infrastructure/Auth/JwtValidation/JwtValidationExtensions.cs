using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Advocacia.Platform.Infrastructure.Auth.JwtValidation;

/// <summary>
/// Configura o JwtBearer para validar o JWT do Supabase Auth via JWKS. "Authority" aponta
/// para o endpoint de descoberta OIDC do Supabase ({url}/auth/v1); o
/// ConfigurationManager interno (Microsoft.IdentityModel.Protocols.OpenIdConnect) resolve
/// as chaves de assinatura automaticamente e as recicla — nenhuma chave fica hardcoded.
///
/// Pré-requisito no projeto Supabase: assinatura assimétrica do JWT (RS256/ES256) precisa
/// estar habilitada (Auth &gt; JWT Keys) — o esquema simétrico legado (HS256, segredo
/// compartilhado) não expõe JWKS e não pode ser validado desta forma.
/// </summary>
public static class JwtValidationExtensions
{
    /// <param name="services">Coleção de serviços do host.</param>
    /// <param name="configuration">Configuração do host (seções "Supabase" e "Jwt").</param>
    /// <param name="configureForTesting">
    /// Seam de teste: permite sobrescrever TokenValidationParameters (ex.: trocar Authority
    /// por uma chave RSA local) sem depender de rede — ver JwtValidationTests.
    /// </param>
    public static IServiceCollection AddSupabaseJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerOptions>? configureForTesting = null)
    {
        var supabaseOptions = configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();

        var jwtOptions = new JwtValidationOptions();
        configuration.GetSection(JwtValidationOptions.SectionName).Bind(jwtOptions);

        if (string.IsNullOrWhiteSpace(jwtOptions.Authority) && !string.IsNullOrWhiteSpace(supabaseOptions.Url))
        {
            jwtOptions.Authority = $"{supabaseOptions.Url.TrimEnd('/')}/auth/v1";
        }

        services.AddSingleton(jwtOptions);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = string.IsNullOrWhiteSpace(jwtOptions.Authority) ? null : jwtOptions.Authority;
                options.Audience = jwtOptions.Audience;
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidIssuer = string.IsNullOrWhiteSpace(jwtOptions.Authority) ? null : jwtOptions.Authority,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = jwtOptions.ValidateLifetime,
                    ClockSkew = jwtOptions.ClockSkew,

                    // O Supabase já usa a claim "role" para seu próprio controle interno
                    // (sempre "authenticated" — usado por auth.role() no Postgres). Nosso
                    // papel de negócio (Owner/Partner/Lawyer/...) vem em "user_role" (ver
                    // Custom Access Token Hook, infra/supabase/migrations/004_custom_access_token_hook.sql),
                    // e mapear RoleClaimType para ela é o que faz [Authorize(Roles=...)]/IsInRole()
                    // do ASP.NET Core enxergarem o papel certo, sem tocar na claim reservada.
                    RoleClaimType = "user_role",
                    NameClaimType = "email",
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Advocacia.Platform.Infrastructure.Auth.JwtValidation");

                        // Nunca logar o token em si — só o tipo/mensagem da falha.
                        logger.LogWarning(context.Exception, "Falha na validação do JWT");

                        return Task.CompletedTask;
                    },
                };

                configureForTesting?.Invoke(options);
            });

        services.AddAuthorization();

        return services;
    }
}
