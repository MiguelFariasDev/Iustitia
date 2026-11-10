namespace Advocacia.Modules.Workflow.Infrastructure.Jobs;

/// <summary>Seção "Jobs:CalendarReminder" do appsettings.</summary>
public sealed class CalendarReminderJobOptions
{
    public const string SectionName = "Jobs:CalendarReminder";

    public bool Enabled { get; set; } = true;

    /// <summary>A cada 5 minutos: a menor janela de erro para o lembrete de 1 hora.</summary>
    public string CronExpression { get; set; } = "*/5 * * * *";
}
