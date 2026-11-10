import 'package:intl/intl.dart';

/// Formatação pt-BR. O app chama `initializeDateFormatting('pt_BR')` na inicialização.
abstract final class Formatters {
  static final _date = DateFormat('dd/MM/yyyy', 'pt_BR');
  static final _dateTime = DateFormat("dd/MM/yyyy 'às' HH:mm", 'pt_BR');
  static final _time = DateFormat('HH:mm', 'pt_BR');
  static final _weekdayLong = DateFormat("EEEE, d 'de' MMMM", 'pt_BR');
  static final _monthYear = DateFormat("MMMM 'de' yyyy", 'pt_BR');
  static final _currency = NumberFormat.currency(locale: 'pt_BR', symbol: r'R$');

  static String date(DateTime value) => _date.format(value.toLocal());
  static String dateTime(DateTime value) => _dateTime.format(value.toLocal());
  static String time(DateTime value) => _time.format(value.toLocal());
  static String weekdayLong(DateTime value) => _capitalize(_weekdayLong.format(value));
  static String monthYear(DateTime value) => _capitalize(_monthYear.format(value));
  static String currency(num value) => _currency.format(value).replaceAll(' ', ' ');

  /// "há 5 min", "ontem"… — o suficiente para uma lista de movimentações.
  static String relative(DateTime value, {DateTime? now}) {
    final reference = now ?? DateTime.now();
    final diff = reference.difference(value);

    if (diff.isNegative) return date(value);
    if (diff.inMinutes < 1) return 'agora';
    if (diff.inMinutes < 60) return 'há ${diff.inMinutes} min';
    if (diff.inHours < 24) return 'há ${diff.inHours} h';
    if (diff.inDays == 1) return 'ontem';
    if (diff.inDays < 7) return 'há ${diff.inDays} dias';
    return date(value);
  }

  /// "Bom dia", "Boa tarde" ou "Boa noite", pela hora local.
  static String greeting(DateTime now) {
    final hour = now.hour;
    if (hour < 5) return 'Boa noite';
    if (hour < 12) return 'Bom dia';
    if (hour < 18) return 'Boa tarde';
    return 'Boa noite';
  }

  /// Primeiro nome, para a saudação. Não presume título nem gênero: "Dr." para quem é
  /// advogada estaria errado — a saudação usa "Dr(a)." (ver DashboardPage).
  static String firstName(String fullName) {
    final trimmed = fullName.trim();
    if (trimmed.isEmpty) return '';
    final first = trimmed.split(RegExp(r'\s+')).first;
    return first.isEmpty ? '' : first[0].toUpperCase() + first.substring(1);
  }

  /// Iniciais para o avatar (até duas letras).
  static String initials(String fullName) {
    final parts = fullName.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first[0].toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  static String _capitalize(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);
}
