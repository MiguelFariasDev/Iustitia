using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Legal.Application;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Infrastructure.Integrations;
using Advocacia.Modules.Legal.Infrastructure.Jobs;
using Advocacia.Modules.Legal.Infrastructure.Jobs.RecurringJobs;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Legal.Infrastructure;

/// <summary>
/// Composition root do módulo Legal. Deve ser chamado DEPOIS de
/// <c>AddPlatformInfrastructure</c>, que registra o que é compartilhado (ITenantContext,
/// IDateTimeProvider, interceptors, observabilidade, Hangfire, MediatR pipeline).
///
/// O host precisa chamar também <c>AddCnjIntegration</c> (Integrations.CNJ.Infrastructure):
/// <c>CnjCaptureJob</c> depende de <c>ICnjClient</c>, e este módulo referencia apenas
/// Integrations.CNJ.<em>Application</em> — a porta, não a implementação —, para que um
/// módulo de negócio nunca dependa da infraestrutura de outro módulo.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLegalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddLegalApplication(configuration);

        services.AddDbContext<LegalDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();

            // Os mesmos três interceptors do PlatformDbContext: auditoria (IAuditable),
            // outbox (eventos de domínio na mesma transação — ADR-008) e SET
            // app.current_tenant para o RLS (ADR-004).
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<OutboxInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IProcessoRepository, ProcessoRepository>();
        services.AddScoped<IPublicationRepository, PublicationRepository>();
        services.AddScoped<ILegalUnitOfWork, LegalUnitOfWork>();

        // Telemetria da captura: singleton porque encapsula um Meter (instrumentos do
        // System.Diagnostics.Metrics devem ser criados uma vez por processo).
        services.AddSingleton<CnjCaptureTelemetry>();

        services.Configure<CnjCaptureJobOptions>(configuration.GetSection(CnjCaptureJobOptions.SectionName));
        services.AddScoped<CnjCaptureJob>();

        // Auto-preenchimento do cadastro de processo. O handler depende da PORTA do módulo
        // Legal; o DataJud fica atrás deste adaptador (ver ADR-064).
        services.AddScoped<IConsultaProcessoTribunal, DataJudConsultaProcessoTribunal>();
        services.AddScoped<LegalRecurringJobsRegistrar>();

        return services;
    }
}
