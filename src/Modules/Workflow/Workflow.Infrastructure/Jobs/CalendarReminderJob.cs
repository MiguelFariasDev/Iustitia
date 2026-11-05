using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Calendar;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Workflow.Infrastructure.Jobs;

/// <summary>
/// Envia os lembretes da agenda: 1 dia e 1 hora antes do início, ao responsável pelo evento,
/// como alerta in-app (IAlertPublisher). Roda de poucos em poucos minutos; cada antecedência
/// é registrada no evento ao ser enviada, então reexecutar nunca avisa duas vezes.
///
/// Um evento criado com menos de 1 dia de antecedência recebe só os lembretes que ainda
/// fazem sentido: o de 1 dia é descartado (marcado como enviado) se o evento já está a menos
/// de 1 hora, para não chegar um "amanhã" quando falta meia hora.
///
/// O alerta guarda o título e o horário do evento — nunca descrição, local ou dados do
/// processo. O log só registra contagens e ids, nunca títulos (podem citar partes).
/// </summary>
public sealed class CalendarReminderJob(
    ICalendarEventRepository repository,
    IWorkflowUnitOfWork unitOfWork,
    IAlertPublisher alertPublisher,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<CalendarReminderJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var due = await repository.ListWithDueRemindersAsync(now, cancellationToken);
        var sent = 0;

        foreach (var calendarEvent in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // O publisher e o RLS precisam do tenant do evento (ver CnjCaptureJob).
            tenantContext.SetTenant(calendarEvent.TenantId);

            var timeToStart = calendarEvent.StartAt - now;

            if (calendarEvent.OneDayReminderSentAt is null)
            {
                if (timeToStart > TimeSpan.FromHours(1))
                {
                    sent += await PublishAsync(calendarEvent, ReminderKind.OneDayBefore, cancellationToken);
                }

                calendarEvent.MarkReminderSent(ReminderKind.OneDayBefore, now);
            }

            if (calendarEvent.OneHourReminderSentAt is null && timeToStart <= TimeSpan.FromHours(1))
            {
                sent += await PublishAsync(calendarEvent, ReminderKind.OneHourBefore, cancellationToken);
                calendarEvent.MarkReminderSent(ReminderKind.OneHourBefore, now);
            }

            repository.Update(calendarEvent);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "CalendarReminderJob: {EventCount} eventos avaliados, {ReminderCount} lembretes enviados.",
            due.Count, sent);
    }

    private async Task<int> PublishAsync(CalendarEvent calendarEvent, ReminderKind kind, CancellationToken cancellationToken)
    {
        if (calendarEvent.AssignedUserId is not { } userId)
        {
            return 0;
        }

        var when = kind == ReminderKind.OneDayBefore ? "amanhã" : "em 1 hora";
        var title = $"{TypeLabel(calendarEvent.Type)} {when}: {calendarEvent.Title}";
        var description = calendarEvent.AllDay
            ? calendarEvent.StartAt.ToString("dd/MM/yyyy")
            : calendarEvent.StartAt.ToString("dd/MM/yyyy HH:mm");

        // O alerta é idempotente por (usuário, tipo, referência). Os dois lembretes do mesmo
        // evento precisam de referências DISTINTAS, senão o segundo seria descartado como
        // duplicata do primeiro — daí o id derivado de forma determinística.
        var created = await alertPublisher.PublishAsync(
            new AlertRequest(
                calendarEvent.TenantId, userId, "Other", title, description,
                ReminderReference(calendarEvent.Id.Value, kind)),
            cancellationToken);

        return created ? 1 : 0;
    }

    internal static Guid ReminderReference(Guid eventId, ReminderKind kind)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{eventId:N}:{(int)kind}"));
        return new Guid(hash);
    }

    private static string TypeLabel(CalendarEventType type) => type switch
    {
        CalendarEventType.Prazo => "Prazo",
        CalendarEventType.Audiencia => "Audiência",
        CalendarEventType.Reuniao => "Reunião",
        _ => "Tarefa",
    };
}
