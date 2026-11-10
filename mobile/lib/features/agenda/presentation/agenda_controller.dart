import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../domain/agenda_event.dart';

/// Eventos do mês exibido. A chave é o primeiro dia do mês: cada mês tem a própria entrada de
/// cache, e voltar a um mês já visto não refaz a consulta.
final agendaMonthProvider =
    FutureProvider.autoDispose.family<List<AgendaEvent>, DateTime>((ref, monthStart) {
  final from = DateTime(monthStart.year, monthStart.month);
  final to = DateTime(monthStart.year, monthStart.month + 1);
  return ref.watch(agendaRepositoryProvider).events(from: from, to: to);
});
