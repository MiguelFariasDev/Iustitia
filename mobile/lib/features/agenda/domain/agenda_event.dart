enum AgendaEventType {
  prazo('Prazo'),
  audiencia('Audiência'),
  reuniao('Reunião');

  const AgendaEventType(this.label);
  final String label;
}

class AgendaEvent {
  const AgendaEvent({
    required this.id,
    required this.title,
    required this.type,
    required this.start,
    this.end,
    this.location,
    this.processId,
    this.processNumber,
  });

  final String id;
  final String title;
  final AgendaEventType type;
  final DateTime start;
  final DateTime? end;
  final String? location;
  final String? processId;
  final String? processNumber;
}
