using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Configura o Serilog como provider de logging da aplicação (ver ADR-036). O
/// MinimumLevel/Properties vêm da seção "Serilog" do appsettings, via
/// ReadFrom.Configuration; enrichers de correlação/tenant/usuário/mascaramento e o sink do
/// Console são sempre
/// aplicados em código (não dependem de reflection sobre nomes de tipo no appsettings).
/// O sink do Application Insights só é adicionado quando há connection string configurada
/// — nunca falha o startup por falta de um recurso Azure que ainda não existe (Fase 0).
/// </summary>
public static class SerilogConfiguration
{
    public static IServiceCollection AddAdvocaciaSerilog(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSerilog((_, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.With<CorrelationIdEnricher>()
                .Enrich.With<TenantIdEnricher>()
                .Enrich.With<UserIdEnricher>()
                .Enrich.With<SensitiveDataMaskingEnricher>()
                .Enrich.WithProperty("Application", "Iustitia")
                .Enrich.WithProperty("Environment", environment.EnvironmentName)
                // Health checks rodam a cada poucos segundos (ver HealthCheckJob/orquestrador) —
                // logar cada requisição a /health/* só gera ruído, sem valor de diagnóstico.
                .Filter.ByExcluding(logEvent =>
                    logEvent.Properties.TryGetValue("Path", out var path)
                    && path.ToString().Contains("/health", StringComparison.OrdinalIgnoreCase))
                .WriteTo.Console();

            var applicationInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
            {
                loggerConfiguration.WriteTo.ApplicationInsights(
                    applicationInsightsConnectionString, new TraceTelemetryConverter());
            }
        });

        return services;
    }
}
