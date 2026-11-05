import '../domain/agenda_event.dart';

abstract interface class AgendaRepository {
  /// `true` enquanto os eventos são de demonstração (não há backend de agenda).
  bool get isDemo;

  /// Eventos que começam no intervalo [from, to).
  Future<List<AgendaEvent>> events({required DateTime from, required DateTime to});
}

/// Agenda de DEMONSTRAÇÃO. O backend ainda não tem agenda, prazos nem tarefas (Fases 3 e 4):
/// os eventos são gerados em torno de "hoje" para a tela poder ser validada, e a tela avisa.
///
/// TODO(agenda-backend): trocar por `GET /agenda/events?from=&to=` e remover o aviso.
class DemoAgendaRepository implements AgendaRepository {
  DemoAgendaRepository({DateTime Function()? now}) : _now = now ?? DateTime.now;

  final DateTime Function() _now;

  @override
  bool get isDemo => true;

  @override
  Future<List<AgendaEvent>> events({required DateTime from, required DateTime to}) async {
    final today = DateTime(_now().year, _now().month, _now().day);

    DateTime at(int days, int hour, [int minute = 0]) =>
        DateTime(today.year, today.month, today.day + days, hour, minute);

    final all = [
      AgendaEvent(
        id: 'demo-1',
        title: 'Prazo: contestação',
        type: AgendaEventType.prazo,
        start: at(1, 18),
        processId: 'proc-001',
        processNumber: '5001001-10.2026.8.26.0001',
      ),
      AgendaEvent(
        id: 'demo-2',
        title: 'Audiência de conciliação',
        type: AgendaEventType.audiencia,
        start: at(3, 14),
        end: at(3, 15),
        location: 'Fórum João Mendes, sala 12',
        processId: 'proc-002',
        processNumber: '5002002-20.2026.5.02.0001',
      ),
      AgendaEvent(
        id: 'demo-3',
        title: 'Reunião com cliente',
        type: AgendaEventType.reuniao,
        start: at(3, 10),
        end: at(3, 11),
        location: 'Escritório',
      ),
      AgendaEvent(
        id: 'demo-4',
        title: 'Prazo: recurso de apelação',
        type: AgendaEventType.prazo,
        start: at(8, 18),
        processId: 'proc-001',
        processNumber: '5001001-10.2026.8.26.0001',
      ),
      AgendaEvent(
        id: 'demo-5',
        title: 'Audiência de instrução',
        type: AgendaEventType.audiencia,
        start: at(-2, 9, 30),
        processId: 'proc-002',
        processNumber: '5002002-20.2026.5.02.0001',
      ),
    ];

    return all.where((e) => !e.start.isBefore(from) && e.start.isBefore(to)).toList()
      ..sort((a, b) => a.start.compareTo(b.start));
  }
}
