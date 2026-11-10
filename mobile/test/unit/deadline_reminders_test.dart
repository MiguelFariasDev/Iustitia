import 'package:advocacia_mobile/core/notifications/deadline_reminders.dart';
import 'package:advocacia_mobile/features/agenda/domain/agenda_event.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

AgendaEvent event(String id, AgendaEventType type, DateTime start, {String? processId}) =>
    AgendaEvent(id: id, title: 'Evento $id', type: type, start: start, processId: processId);

void main() {
  final now = DateTime(2026, 11, 4, 12);

  group('reminderTime', () {
    test('lembra 24h antes por padrão', () {
      expect(reminderTime(DateTime(2026, 11, 10, 18), now: now), DateTime(2026, 11, 9, 18));
    });

    test('momento que já passou não gera lembrete', () {
      expect(reminderTime(DateTime(2026, 11, 5, 8), now: now), isNull); // 24h antes = ontem
    });

    test('respeita a antecedência pedida', () {
      expect(
        reminderTime(DateTime(2026, 11, 5, 8), lead: const Duration(hours: 2), now: now),
        DateTime(2026, 11, 5, 6),
      );
    });
  });

  group('DeadlineReminderScheduler', () {
    test('agenda prazos e audiências, ignora reuniões e o que já passou', () async {
      final notifications = FakeLocalNotifications();
      final scheduler = DeadlineReminderScheduler(notifications);

      final count = await scheduler.sync([
        event('a', AgendaEventType.prazo, DateTime(2026, 11, 10, 18), processId: 'p1'),
        event('b', AgendaEventType.audiencia, DateTime(2026, 11, 12, 14)),
        event('c', AgendaEventType.reuniao, DateTime(2026, 11, 12, 10)),
        event('d', AgendaEventType.prazo, DateTime(2026, 11, 4, 18)), // já passou
      ], now: now);

      expect(count, 2);
      expect(notifications.scheduled.map((s) => s.title), ['Evento a', 'Evento b']);
    });

    test('o destino do lembrete é o processo, ou a agenda quando não há processo', () async {
      final notifications = FakeLocalNotifications();

      await DeadlineReminderScheduler(notifications).sync([
        event('a', AgendaEventType.prazo, DateTime(2026, 11, 10), processId: 'p1'),
        event('b', AgendaEventType.prazo, DateTime(2026, 11, 11)),
      ], now: now);

      expect(notifications.scheduled.map((s) => s.payload), ['/processos/p1', '/agenda']);
    });

    test('reagendar substitui os lembretes anteriores', () async {
      final notifications = FakeLocalNotifications();
      final scheduler = DeadlineReminderScheduler(notifications);
      final prazo = event('a', AgendaEventType.prazo, DateTime(2026, 11, 10));

      await scheduler.sync([prazo], now: now);
      await scheduler.sync([prazo], now: now);

      expect(notifications.cancelAllCalls, 2);
      expect(notifications.scheduled, hasLength(1)); // nunca dois lembretes do mesmo evento
    });

    test('o mesmo evento sempre gera o mesmo id', () async {
      final a = FakeLocalNotifications();
      final b = FakeLocalNotifications();
      final prazo = event('estável', AgendaEventType.prazo, DateTime(2026, 11, 10));

      await DeadlineReminderScheduler(a).sync([prazo], now: now);
      await DeadlineReminderScheduler(b).sync([prazo], now: now);

      expect(a.scheduled.single.id, b.scheduled.single.id);
      expect(a.scheduled.single.id, isNonNegative);
    });
  });
}
