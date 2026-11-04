using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.Infrastructure.Jobs.HangfireConfiguration;

/// <summary>Configura o Hangfire com storage no PostgreSQL (ver ADR-017).</summary>
public static class HangfireSetup
{
    public static IServiceCollection AddAdvocaciaHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));
        var options = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>() ?? new HangfireOptions();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres => postgres.UseNpgsqlConnection(connectionString)));

        // Só o host configurado para processar sobe um servidor Hangfire. O storage e o
        // client são registrados sempre, para qualquer host poder enfileirar e para o
        // dashboard funcionar na API.
        if (options.ProcessJobs)
        {
            services.AddHangfireServer(server =>
            {
                server.WorkerCount = options.WorkerCount;
                server.ServerTimeout = TimeSpan.FromMinutes(30);
            });
        }

        return services;
    }
}
