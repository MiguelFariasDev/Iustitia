using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;

/// <summary>
/// Registra os health checks de dependências críticas (ver ADR-036 e
/// docs/observability/health-checks.md): PostgreSQL, Redis e a outbox. O check do
/// Azure Service Bus/RabbitMQ não precisa de implementação própria — o MassTransit já
/// registra um health check próprio ("masstransit-bus", tag "ready") assim que
/// AddAdvocaciaMessaging roda, desde que AddHealthChecks já tenha sido chamado.
/// </summary>
public static class HealthChecksConfiguration
{
    public static IHealthChecksBuilder AddAdvocaciaHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HealthChecksOptions>(configuration.GetSection(HealthChecksOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6380";

        return services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"])
            .AddRedis(redisConnectionString, name: "redis", tags: ["ready"])
            .AddCheck<OutboxHealthCheck>("outbox", tags: ["ready"]);
    }
}
