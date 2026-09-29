using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Configura o MassTransit (ver ADR-016): RabbitMQ em desenvolvimento local (via
/// docker-compose, ver infra/docker) ou Azure Service Bus em produção, escolhido pela
/// seção "Messaging:Transport" do appsettings — nunca pelo ambiente ASP.NET Core
/// diretamente, para manter esta camada independente de hosting web/worker.
/// Retry exponencial (5 tentativas) e dead-letter automática (fila de erro do MassTransit
/// no RabbitMQ; DLQ nativa no Azure Service Bus) são aplicados a todos os endpoints.
/// </summary>
public static class MassTransitConfiguration
{
    public static IServiceCollection AddAdvocaciaMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var options = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();
        services.AddSingleton(options);

        services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();

        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);

            bus.SetKebabCaseEndpointNameFormatter();

            if (string.Equals(options.Transport, "RabbitMq", StringComparison.OrdinalIgnoreCase))
            {
                bus.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(options.RabbitMq.Host, options.RabbitMq.Port, options.RabbitMq.VirtualHost, host =>
                    {
                        host.Username(options.RabbitMq.Username);
                        host.Password(options.RabbitMq.Password);
                    });

                    ConfigureRetryAndEndpoints(cfg, context);
                });
            }
            else
            {
                var connectionString = options.AzureServiceBusConnectionString
                    ?? throw new InvalidOperationException(
                        "Messaging:AzureServiceBusConnectionString não configurada (obrigatória quando Messaging:Transport = AzureServiceBus).");

                bus.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(connectionString);
                    ConfigureRetryAndEndpoints(cfg, context);
                });
            }
        });

        return services;
    }

    private static void ConfigureRetryAndEndpoints<TEndpoint>(IBusFactoryConfigurator<TEndpoint> cfg, IBusRegistrationContext context)
        where TEndpoint : IReceiveEndpointConfigurator
    {
        cfg.UseMessageRetry(retry => retry.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(2),
            maxInterval: TimeSpan.FromSeconds(60),
            intervalDelta: TimeSpan.FromSeconds(2)));

        cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);

        cfg.ConfigureEndpoints(context);
    }
}
