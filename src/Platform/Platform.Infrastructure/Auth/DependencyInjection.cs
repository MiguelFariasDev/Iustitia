using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Advocacia.Platform.Infrastructure.Auth.Identity;
using Advocacia.Platform.Infrastructure.Auth.JwtValidation;
using Advocacia.Platform.Infrastructure.Auditing.AuditService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SupabaseAuthServiceImpl = Advocacia.Platform.Infrastructure.Auth.SupabaseAuthService.SupabaseAuthService;

namespace Advocacia.Platform.Infrastructure.Auth;

/// <summary>
/// Composition root de autenticação/autorização. Precisa ser chamado DEPOIS de
/// AddBuildingBlocksInfrastructure/AddPlatformInfrastructure: ICurrentUser/ITenantContext
/// registrados aqui (CurrentUserAccessor baseado em HttpContext) substituem os padrões
/// seguros (AnonymousCurrentUser) — a última registração de um serviço vence na resolução
/// padrão do container do ASP.NET Core.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseOptions>(configuration.GetSection(SupabaseOptions.SectionName));
        var supabaseOptions = configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        services.AddScoped<IAuditService, AuditService>();

        services
            .AddHttpClient<ISupabaseAuthService, SupabaseAuthServiceImpl>(client =>
            {
                if (!string.IsNullOrWhiteSpace(supabaseOptions.Url))
                {
                    client.BaseAddress = new Uri(supabaseOptions.Url);
                }

                // Toda chamada à API do Supabase Auth exige o header apikey, além do
                // Authorization Bearer específico de cada operação (ver SupabaseAuthService).
                client.DefaultRequestHeaders.Remove("apikey");
                client.DefaultRequestHeaders.Add("apikey", supabaseOptions.AnonKey);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddSupabaseJwtAuthentication(configuration);
        services.AddPlatformAuthorizationPolicies();

        return services;
    }
}
