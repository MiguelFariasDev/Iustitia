namespace Advocacia.Platform.Infrastructure.Jobs;

/// <summary>Habilitação/agendamento de cada job recorrente — seção "Jobs" do appsettings.</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// Hangfire (via NCrontab) só suporta cron de 5 campos (granularidade de minuto) — não
    /// há suporte nativo a segundos. "*/10 * * * * *" do prompt original não é um cron válido
    /// aqui; "* * * * *" (a cada minuto) é o intervalo mínimo real de agendamento.
    /// </summary>
    public JobOptions OutboxProcessor { get; init; } = new() { CronExpression = "* * * * *" };

    public JobOptions OutboxCleanup { get; init; } = new() { CronExpression = "0 3 * * *" };

    public JobOptions AuditLogCleanup { get; init; } = new() { CronExpression = "0 4 * * 0" };

    public JobOptions HealthCheck { get; init; } = new() { CronExpression = "*/5 * * * *" };

    public sealed class JobOptions
    {
        public bool Enabled { get; init; } = true;

        public string CronExpression { get; init; } = "*/10 * * * * *";
    }
}
