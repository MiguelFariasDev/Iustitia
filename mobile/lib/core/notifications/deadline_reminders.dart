import '../../features/agenda/domain/agenda_event.dart';
import 'local_notifications.dart';

/// Quando lembrar de um prazo: [lead] antes do início. `null` se esse momento já passou
/// (lembrar do passado dispararia na hora, o que é só ruído).
DateTime? reminderTime(DateTime start, {Duration lead = const Duration(hours: 24), DateTime? now}) {
  final when = start.subtract(lead);
  return when.isAfter(now ?? DateTime.now()) ? when : null;
}

/// Agenda um lembrete por prazo/audiência. Reagendar substitui os anteriores: cancela tudo e
/// recria, em vez de tentar descobrir o que mudou.
class DeadlineReminderScheduler {
  DeadlineReminderScheduler(this._notifications);

  final LocalNotifications _notifications;

  Future<int> sync(
    List<AgendaEvent> events, {
    Duration lead = const Duration(hours: 24),
    DateTime? now,
  }) async {
    await _notifications.cancelAll();

    var scheduled = 0;
    for (final event in events) {
      if (event.type == AgendaEventType.reuniao) continue;

      final when = reminderTime(event.start, lead: lead, now: now);
      if (when == null) continue;

      await _notifications.scheduleAt(
        // Id estável por evento: o mesmo evento nunca gera dois lembretes.
        id: event.id.hashCode & 0x7fffffff,
        when: when,
        title: event.title,
        body: event.processNumber,
        payload: event.processId == null ? '/agenda' : '/processos/${event.processId}',
      );
      scheduled++;
    }
    return scheduled;
  }
}
