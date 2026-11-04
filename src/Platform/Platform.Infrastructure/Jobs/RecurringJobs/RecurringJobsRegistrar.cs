using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Jobs.RecurringJobs;

/// <summary>
/// Registra os jobs recorrentes do Platform no Hangfire, a partir da seção "Jobs" do
/// appsettings. Chamado uma vez na inicialização do Worker (ver Hosts/Worker/Program.cs).
/// Módulos de negócio (Legal/Workflow/...) registram os seus próprios jobs recorrentes da
/// mesma forma a partir da Fase 1+.
/// </summary>
public sealed class RecurringJobsRegistrar(IRecurringJobManager recurringJobManager, IOptions<JobsOptions> options)
{
    public void RegisterAll()
    {
        var jobs = options.Value;

        if (jobs.OutboxProcessor.Enabled)
        {
            recurringJobManager.AddOrUpdate<OutboxProcessorJob>(
                "outbox-processor", job => job.RunAsync(CancellationToken.None), jobs.OutboxProcessor.CronExpression);
        }

        if (jobs.OutboxCleanup.Enabled)
        {
            recurringJobManager.AddOrUpdate<OutboxCleanupJob>(
                "outbox-cleanup", job => job.RunAsync(CancellationToken.None), jobs.OutboxCleanup.CronExpression);
        }

        if (jobs.AuditLogCleanup.Enabled)
        {
            recurringJobManager.AddOrUpdate<AuditLogCleanupJob>(
                "audit-log-cleanup", job => job.RunAsync(CancellationToken.None), jobs.AuditLogCleanup.CronExpression);
        }

        if (jobs.HealthCheck.Enabled)
        {
            recurringJobManager.AddOrUpdate<HealthCheckJob>(
                "health-check", job => job.RunAsync(CancellationToken.None), jobs.HealthCheck.CronExpression);
        }
    }
}
