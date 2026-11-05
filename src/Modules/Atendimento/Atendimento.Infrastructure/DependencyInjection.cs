using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Atendimentos.Application;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Infrastructure.Bot;
using Advocacia.Modules.Atendimentos.Infrastructure.Messaging;
using Advocacia.Modules.Atendimentos.Infrastructure.Persistence;
using Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Atendimentos.Infrastructure;

/// <summary>
/// Composition root do módulo Atendimento. Deve ser chamado DEPOIS de
/// <c>AddPlatformInfrastructure</c>, que registra o que é compartilhado (ITenantContext,
/// IDateTimeProvider, interceptors, IRealtimeNotifier, MassTransit).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAtendimentoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddAtendimentoApplication();

        services.AddDbContext<AtendimentoDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();

            // Mesmos três interceptors dos demais módulos: auditoria (IAuditable), outbox
            // (eventos na mesma transação — ADR-008) e SET app.current_tenant para o RLS (ADR-004).
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<OutboxInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<IAtendimentoRepository, AtendimentoRepository>();
        services.AddScoped<IBotApiKeyRepository, BotApiKeyRepository>();
        services.AddScoped<IAtendimentoUnitOfWork, AtendimentoUnitOfWork>();
        services.AddScoped<IBotCredentialValidator, BotCredentialValidator>();

        services.Configure<BotGatewayOptions>(configuration.GetSection(BotGatewayOptions.SectionName));
        services.AddHttpClient<IBotGateway, BotGateway>(client => client.Timeout = TimeSpan.FromSeconds(15))
            .AddStandardResilienceHandler(options =>
            {
                // Envio de mensagem não é seguro para repetir às cegas. A idempotência do Bot
                // (por messageId) cobre um retry, mas o ideal é nem tentar: quem decide
                // reenviar é o advogado, vendo a mensagem marcada como Failed.
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            });

        return services;
    }

    /// <summary>
    /// Registra os consumers de Realtime no bus do MassTransit. Passar ao gancho
    /// <c>configureModuleConsumers</c> de <c>AddPlatformInfrastructure</c> — AddMassTransit só
    /// pode ser chamado uma vez por container.
    /// </summary>
    public static IBusRegistrationConfigurator AddAtendimentoConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<AtendimentoIniciadoConsumer>();
        bus.AddConsumer<AtendimentoStatusAlteradoConsumer>();
        bus.AddConsumer<AtendimentoClienteVinculadoConsumer>();
        bus.AddConsumer<AtendimentoMensagemAdicionadaConsumer>();
        return bus;
    }
}
