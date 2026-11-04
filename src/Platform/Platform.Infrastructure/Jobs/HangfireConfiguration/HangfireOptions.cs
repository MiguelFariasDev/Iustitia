namespace Advocacia.Platform.Infrastructure.Jobs.HangfireConfiguration;

/// <summary>Configuração do Hangfire — seção "Hangfire" do appsettings (ver ADR-017).</summary>
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    public bool DashboardEnabled { get; init; } = true;

    public string DashboardPath { get; init; } = "/jobs";

    public int WorkerCount { get; init; } = 4;

    /// <summary>
    /// Se ESTE host executa jobs, e não apenas os enfileira. Falso por padrão: quem processa
    /// é o Worker, que é o único host que registra os jobs recorrentes (ver
    /// RecurringJobsRegistrar e LegalRecurringJobsRegistrar).
    ///
    /// Um host que PROCESSA sem REGISTRAR é perigoso: ele executa o que estiver no storage,
    /// inclusive um job recorrente antigo que a configuração atual já desabilitou — foi
    /// assim que uma varredura de tribunal desativada voltou a rodar e trouxe publicações de
    /// processos de terceiros para a base de um escritório.
    /// </summary>
    public bool ProcessJobs { get; init; }
}
