using Hangfire;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Workflow.Infrastructure.Jobs;

/// <summary>
/// Registra os jobs recorrentes do módulo Workflow no Hangfire, ao lado do registrar do
/// Platform e do Legal (ver Hosts/Worker/Program.cs). Job desabilitado é REMOVIDO do storage.
/// </summary>
public sealed class WorkflowRecurringJobsRegistrar(
    IRecurringJobManager recurringJobManager, IOptions<CalendarReminderJobOptions> reminderOptions)
{
    public const string CalendarReminderJobId = "calendar-reminders";

    public void RegisterAll()
    {
        var options = reminderOptions.Value;

        if (options.Enabled)
        {
            recurringJobManager.AddOrUpdate<CalendarReminderJob>(
                CalendarReminderJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                options.CronExpression);
        }
        else
        {
            recurringJobManager.RemoveIfExists(CalendarReminderJobId);
        }
    }
}
