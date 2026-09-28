using Advocacia.BuildingBlocks.Application.Abstractions;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Configura OpenTelemetry (traces + métricas — ver ADR-036): instrumentação automática
/// (ASP.NET Core, HttpClient, Runtime) + os ActivitySource/Meter customizados desta
/// aplicação (ver ActivitySourceNames/MeterNames/CustomMetrics). O exporter para Azure
/// Monitor só é ligado quando há connection string configurada (ver
/// <c>ApplicationInsights:ConnectionString</c>) — sem isso, os dados de telemetria ainda
/// ficam disponíveis localmente via /metrics (Prometheus, ver Hosts/Api) e nada quebra
/// por falta de um recurso Azure que ainda não existe (Fase 0).
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddAdvocaciaObservability(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IAppMetrics, CustomMetrics>();

        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Advocacia";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "0.1.0";
        var applicationInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

        var openTelemetryBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddSource(
                    ActivitySourceNames.Outbox,
                    ActivitySourceNames.Jobs,
                    ActivitySourceNames.Handlers,
                    ActivitySourceNames.Consumers)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(MeterNames.Advocacia)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            openTelemetryBuilder.UseAzureMonitor(options => options.ConnectionString = applicationInsightsConnectionString);
        }

        return services;
    }
}
