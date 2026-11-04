import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../app/theme.dart';
import '../../../core/providers.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/states.dart';
import '../../../shared/widgets/status_badge.dart';
import '../domain/agenda_event.dart';
import 'agenda_controller.dart';

enum _ViewMode { month, week }

BadgeTone _toneOf(AgendaEventType type) => switch (type) {
      AgendaEventType.prazo => BadgeTone.danger,
      AgendaEventType.audiencia => BadgeTone.info,
      AgendaEventType.reuniao => BadgeTone.success,
    };

/// Cor do ponto na grade. Só reforça: o tipo é sempre dito por texto (lista e leitor de tela).
Color _dotColor(AgendaEventType type) => switch (type) {
      AgendaEventType.prazo => AppColors.danger,
      AgendaEventType.audiencia => AppColors.electric,
      AgendaEventType.reuniao => AppColors.success,
    };

DateTime _day(DateTime d) => DateTime(d.year, d.month, d.day);

/// Domingo da semana de [d]: o calendário brasileiro começa no domingo.
DateTime _weekStart(DateTime d) => _day(d).subtract(Duration(days: d.weekday % 7));

class AgendaPage extends ConsumerStatefulWidget {
  const AgendaPage({super.key});

  @override
  ConsumerState<AgendaPage> createState() => _AgendaPageState();
}

class _AgendaPageState extends ConsumerState<AgendaPage> {
  // Estado puramente de apresentação desta tela (mês exibido, dia e modo): não é compartilhado.
  _ViewMode _mode = _ViewMode.month;
  late DateTime _selected = _day(ref.read(clockProvider)());

  DateTime get _month => DateTime(_selected.year, _selected.month);

  void _shift(int direction) => setState(() {
        _selected = _mode == _ViewMode.month
            ? DateTime(_selected.year, _selected.month + direction, 1)
            : _selected.add(Duration(days: 7 * direction));
      });

  @override
  Widget build(BuildContext context) {
    final events = ref.watch(agendaMonthProvider(_month));
    final theme = Theme.of(context);

    // A semana pode cruzar dois meses: pede os dois para os pontos não sumirem.
    final weekEnd = _weekStart(_selected).add(const Duration(days: 6));
    final nextMonth = events.value == null || weekEnd.month == _selected.month
        ? null
        : ref.watch(agendaMonthProvider(DateTime(weekEnd.year, weekEnd.month)));
    final all = [...?events.value, ...?nextMonth?.value];
    final dayEvents = all.where((e) => _day(e.start) == _selected).toList();

    return Scaffold(
      appBar: AppBar(title: const Text('Agenda')),
      body: switch (events) {
        AsyncError(:final error) =>
          ErrorView(error: error, onRetry: () => ref.invalidate(agendaMonthProvider(_month))),
        _ => ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (ref.watch(agendaRepositoryProviderIsDemo)) ...[
                const DemoNotice(module: 'A agenda'),
                const SizedBox(height: 16),
              ],
              SegmentedButton<_ViewMode>(
                segments: const [
                  ButtonSegment(value: _ViewMode.month, label: Text('Mês'), icon: Icon(Icons.calendar_view_month)),
                  ButtonSegment(value: _ViewMode.week, label: Text('Semana'), icon: Icon(Icons.calendar_view_week)),
                ],
                selected: {_mode},
                onSelectionChanged: (s) => setState(() => _mode = s.first),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  IconButton(
                    tooltip: _mode == _ViewMode.month ? 'Mês anterior' : 'Semana anterior',
                    icon: const Icon(Icons.chevron_left),
                    onPressed: () => _shift(-1),
                  ),
                  Expanded(
                    child: Semantics(
                      header: true,
                      liveRegion: true,
                      child: Text(
                        Formatters.monthYear(_selected),
                        textAlign: TextAlign.center,
                        style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                      ),
                    ),
                  ),
                  IconButton(
                    tooltip: _mode == _ViewMode.month ? 'Próximo mês' : 'Próxima semana',
                    icon: const Icon(Icons.chevron_right),
                    onPressed: () => _shift(1),
                  ),
                ],
              ),
              _WeekdayHeader(),
              _CalendarGrid(
                mode: _mode,
                selected: _selected,
                events: all,
                today: _day(ref.read(clockProvider)()),
                onSelect: (d) => setState(() => _selected = d),
              ),
              const SizedBox(height: 16),
              Semantics(
                header: true,
                child: Text(
                  Formatters.weekdayLong(_selected),
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
              ),
              const SizedBox(height: 8),
              if (events.isLoading && events.value == null)
                const LoadingView(label: 'Carregando agenda…')
              else if (dayEvents.isEmpty)
                const EmptyView(icon: Icons.event_busy_outlined, title: 'Nada neste dia')
              else
                for (final e in dayEvents)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: _EventTile(event: e, onTap: () => _showDetails(context, e)),
                  ),
            ],
          ),
      },
    );
  }

  void _showDetails(BuildContext context, AgendaEvent event) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) {
        final theme = Theme.of(sheetContext);
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                StatusBadge(label: event.type.label, tone: _toneOf(event.type)),
                const SizedBox(height: 12),
                Semantics(
                  header: true,
                  child: Text(event.title, style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
                ),
                const SizedBox(height: 12),
                _DetailLine(icon: Icons.schedule, text: Formatters.dateTime(event.start)),
                if (event.location != null) _DetailLine(icon: Icons.place_outlined, text: event.location!),
                if (event.processNumber != null)
                  _DetailLine(icon: Icons.gavel_outlined, text: event.processNumber!),
                if (event.processId != null) ...[
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: () {
                      Navigator.of(sheetContext).pop();
                      context.go(AppRoutes.processo(event.processId!));
                    },
                    child: const Text('Abrir processo'),
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

class _DetailLine extends StatelessWidget {
  const _DetailLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(children: [
          ExcludeSemantics(child: Icon(icon, size: 20)),
          const SizedBox(width: 12),
          Expanded(child: Text(text)),
        ]),
      );
}

class _WeekdayHeader extends StatelessWidget {
  static const _letters = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];
  static const _names = ['domingo', 'segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado'];

  @override
  Widget build(BuildContext context) => Row(
        children: [
          for (var i = 0; i < 7; i++)
            Expanded(
              child: Semantics(
                label: _names[i],
                excludeSemantics: true,
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Center(
                    child: Text(
                      _letters[i],
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                    ),
                  ),
                ),
              ),
            ),
        ],
      );
}

class _CalendarGrid extends StatelessWidget {
  const _CalendarGrid({
    required this.mode,
    required this.selected,
    required this.events,
    required this.today,
    required this.onSelect,
  });

  final _ViewMode mode;
  final DateTime selected;
  final List<AgendaEvent> events;
  final DateTime today;
  final ValueChanged<DateTime> onSelect;

  List<DateTime> get _days {
    if (mode == _ViewMode.week) {
      final start = _weekStart(selected);
      return [for (var i = 0; i < 7; i++) start.add(Duration(days: i))];
    }
    final first = DateTime(selected.year, selected.month);
    final start = _weekStart(first);
    final last = DateTime(selected.year, selected.month + 1, 0);
    final count = ((last.difference(start).inDays + 1) / 7).ceil() * 7;
    return [for (var i = 0; i < count; i++) start.add(Duration(days: i))];
  }

  @override
  Widget build(BuildContext context) {
    final days = _days;

    return Column(
      children: [
        for (var row = 0; row < days.length / 7; row++)
          Row(
            children: [
              for (var col = 0; col < 7; col++)
                Expanded(
                  child: _DayCell(
                    day: days[row * 7 + col],
                    inMonth: mode == _ViewMode.week || days[row * 7 + col].month == selected.month,
                    isToday: days[row * 7 + col] == today,
                    isSelected: days[row * 7 + col] == selected,
                    events: events.where((e) => _day(e.start) == days[row * 7 + col]).toList(),
                    onTap: () => onSelect(days[row * 7 + col]),
                  ),
                ),
            ],
          ),
      ],
    );
  }
}

class _DayCell extends StatelessWidget {
  const _DayCell({
    required this.day,
    required this.inMonth,
    required this.isToday,
    required this.isSelected,
    required this.events,
    required this.onTap,
  });

  final DateTime day;
  final bool inMonth;
  final bool isToday;
  final bool isSelected;
  final List<AgendaEvent> events;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final types = events.map((e) => e.type).toSet().toList();

    final label = [
      Formatters.weekdayLong(day),
      if (isToday) 'hoje',
      events.isEmpty ? 'sem eventos' : '${events.length} ${events.length == 1 ? 'evento' : 'eventos'}',
    ].join(', ');

    return Semantics(
      button: true,
      selected: isSelected,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          // 48dp de altura: o piso de alvo de toque acessível.
          height: 52,
          margin: const EdgeInsets.all(2),
          decoration: BoxDecoration(
            color: isSelected ? theme.colorScheme.primary : null,
            borderRadius: BorderRadius.circular(12),
            border: isToday && !isSelected ? Border.all(color: theme.colorScheme.primary, width: 1.5) : null,
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                '${day.day}',
                style: theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: isToday ? FontWeight.w700 : null,
                  color: isSelected
                      ? theme.colorScheme.onPrimary
                      : inMonth
                          ? theme.colorScheme.onSurface
                          : theme.colorScheme.onSurfaceVariant.withValues(alpha: 0.5),
                ),
              ),
              const SizedBox(height: 4),
              SizedBox(
                height: 6,
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (final t in types)
                      Container(
                        width: 6,
                        height: 6,
                        margin: const EdgeInsets.symmetric(horizontal: 1),
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          // Sobre o dia selecionado (fundo escuro) os pontos ficam brancos.
                          color: isSelected ? theme.colorScheme.onPrimary : _dotColor(t),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EventTile extends StatelessWidget {
  const _EventTile({required this.event, required this.onTap});

  final AgendaEvent event;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Semantics(
      button: true,
      label: '${event.type.label}, ${event.title}, ${Formatters.time(event.start)}'
          '${event.location == null ? '' : ', ${event.location}'}',
      excludeSemantics: true,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                Column(
                  children: [
                    Text(Formatters.time(event.start), style: theme.textTheme.titleSmall),
                    if (event.end != null)
                      Text(
                        Formatters.time(event.end!),
                        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                  ],
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(event.title, style: theme.textTheme.bodyLarge),
                      if (event.location != null)
                        Text(
                          event.location!,
                          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                        ),
                    ],
                  ),
                ),
                StatusBadge(label: event.type.label, tone: _toneOf(event.type)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
