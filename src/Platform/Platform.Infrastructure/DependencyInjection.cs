using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;
using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Infrastructure.Alerts;
using Advocacia.Platform.Infrastructure.Auth;
using Advocacia.Platform.Infrastructure.FeatureFlags;
using Advocacia.Platform.Infrastructure.Jobs;
using Advocacia.Platform.Infrastructure.Jobs.HangfireConfiguration;
using Advocacia.Platform.Infrastructure.Jobs.RecurringJobs;
using Advocacia.Platform.Infrastructure.Messaging.Consumers;
using Advocacia.Platform.Infrastructure.Notifications.EmailService;
using Advocacia.Platform.Infrastructure.Persistence;
using Advocacia.Platform.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Infrastructure.Push;
using Advocacia.Platform.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure;

/// <summary>Composition root da infraestrutura de Platform, chamado pelos Hosts (Api/Worker).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddBuildingBlocksInfrastructure(configuration, environment);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddScoped<AuditInterceptor>();
        services.AddScoped<OutboxInterceptor>();
        services.AddScoped<TenantContextInterceptor>();

        services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<OutboxInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        // Repository<T>/UnitOfWork dependem do DbContext base (reutilizável por qualquer
        // módulo); resolve para a instância de PlatformDbContext registrada no escopo.
        services.AddScoped<EfDbContext>(serviceProvider => serviceProvider.GetRequiredService<PlatformDbContext>());

        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(ReadRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<OutboxProcessorOptions>(configuration.GetSection(OutboxProcessorOptions.SectionName));
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IOutboxCleanupService, OutboxCleanupService>();
        services.AddSingleton<IEventTypeResolver, EventTypeResolver>();

        // Mensageria (ver ADR-016): consumers de integration/domain events publicados via
        // outbox. Cada consumer entra aqui — módulos de negócio registram os seus próprios
        // consumers do mesmo jeito, a partir da Fase 1+.
        services.AddAdvocaciaMessaging(configuration, bus =>
        {
            bus.AddConsumer<UserInvitedConsumer>();
            bus.AddConsumer<TenantCreatedConsumer>();
            bus.AddConsumer<AuditLogCreatedConsumer>();
            bus.AddConsumer<AlertCreatedConsumer>();
        });

        services.AddScoped<INotificationService, EmailService>();

        // Alertas in-app (ADR-065): repositório, porta de criação para os módulos e push Realtime.
        services.Configure<SupabaseOptions>(configuration.GetSection(SupabaseOptions.SectionName));
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertPublisher, AlertPublisher>();
        services.AddHttpClient<IRealtimeNotifier, SupabaseRealtimeNotifier>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        // Dispositivos móveis e push (ADR-069). O envio via FCM só liga com credenciais do Firebase
        // (Push:Fcm); sem elas, o serviço é o Noop e o restante do sistema segue igual. O cache em
        // memória serve o guarda de dispositivo revogado (RevokedDeviceMiddleware).
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddMemoryCache();
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddPushNotifications(configuration.GetSection(PushOptions.SectionName).Get<PushOptions>() ?? new PushOptions());

        // Jobs (ver ADR-017): Hangfire com storage no PostgreSQL. RecurringJobsRegistrar é
        // chamado explicitamente pelo Worker após o host subir (precisa do storage já
        // inicializado) — ver Hosts/Worker/Program.cs.
        services.Configure<JobsOptions>(configuration.GetSection(JobsOptions.SectionName));
        services.AddAdvocaciaHangfire(configuration);
        services.AddScoped<RecurringJobsRegistrar>();

        // Observabilidade (ver ADR-036): health checks de dependências críticas.
        services.AddAdvocaciaHealthChecks(configuration);

        // Feature Flags por tenant (ver ADR-038): Microsoft.FeatureManagement cobre toggles
        // globais simples via appsettings; IFeatureFlagService é o serviço próprio, por
        // tenant, com cache Redis, usado pelos módulos para checar se uma funcionalidade
        // está liberada para um escritório específico.
        services.AddFeatureManagement();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // Auth (ICurrentUser real, ISupabaseAuthService, IAuditService, JWT, policies) é
        // registrado separadamente via AddPlatformAuth — ver Auth/DependencyInjection.cs.
        // Precisa ser chamado DEPOIS deste método no host (substitui os padrões seguros
        // AnonymousCurrentUser/AsyncLocalTenantContext).
        //
        // Settings e FeatureFlags não têm um serviço de infraestrutura próprio: Setting e
        // FeatureFlag são AggregateRoot (ver Etapa 0.4), então os handlers de
        // Platform.Application usam IRepository<Setting, SettingId>/IRepository<FeatureFlag,
        // FeatureFlagId> diretamente — já cobertos pelo registro genérico acima, do mesmo
        // jeito que Tenant/User.

        return services;
    }
}
