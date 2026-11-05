import 'package:advocacia_mobile/core/utils/formatters.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';

void main() {
  setUpAll(() => initializeDateFormatting('pt_BR'));

  group('greeting', () {
    test('muda conforme a hora', () {
      expect(Formatters.greeting(DateTime(2026, 11, 4, 3)), 'Boa noite');
      expect(Formatters.greeting(DateTime(2026, 11, 4, 8)), 'Bom dia');
      expect(Formatters.greeting(DateTime(2026, 11, 4, 12)), 'Boa tarde');
      expect(Formatters.greeting(DateTime(2026, 11, 4, 17, 59)), 'Boa tarde');
      expect(Formatters.greeting(DateTime(2026, 11, 4, 18)), 'Boa noite');
    });
  });

  group('nomes', () {
    test('primeiro nome com inicial maiúscula', () {
      expect(Formatters.firstName('ana souza'), 'Ana');
      expect(Formatters.firstName('  Carlos   Eduardo '), 'Carlos');
      expect(Formatters.firstName(''), '');
    });

    test('iniciais: primeira e última, ou só uma', () {
      expect(Formatters.initials('Maria Aparecida Silva'), 'MS');
      expect(Formatters.initials('ana'), 'A');
      expect(Formatters.initials('  '), '?');
    });
  });

  group('datas e valores', () {
    test('data, hora e moeda em pt-BR', () {
      expect(Formatters.date(DateTime(2026, 11, 4)), '04/11/2026');
      expect(Formatters.time(DateTime(2026, 11, 4, 9, 5)), '09:05');
      expect(Formatters.dateTime(DateTime(2026, 11, 4, 14, 30)), '04/11/2026 às 14:30');
      expect(Formatters.currency(35000), r'R$ 35.000,00');
    });

    test('mês e dia da semana começam com maiúscula', () {
      expect(Formatters.monthYear(DateTime(2026, 11, 4)), 'Novembro de 2026');
      expect(Formatters.weekdayLong(DateTime(2026, 11, 4)), 'Quarta-feira, 4 de novembro');
    });

    test('relative escolhe a unidade pela distância', () {
      final now = DateTime(2026, 11, 4, 12);
      expect(Formatters.relative(now.subtract(const Duration(seconds: 20)), now: now), 'agora');
      expect(Formatters.relative(now.subtract(const Duration(minutes: 5)), now: now), 'há 5 min');
      expect(Formatters.relative(now.subtract(const Duration(hours: 3)), now: now), 'há 3 h');
      expect(Formatters.relative(now.subtract(const Duration(days: 1)), now: now), 'ontem');
      expect(Formatters.relative(now.subtract(const Duration(days: 3)), now: now), 'há 3 dias');
      expect(Formatters.relative(now.subtract(const Duration(days: 30)), now: now), '05/10/2026');
    });

    test('relative com data no futuro mostra a data, não "há -2 h"', () {
      final now = DateTime(2026, 11, 4, 12);
      expect(Formatters.relative(now.add(const Duration(hours: 2)), now: now), '04/11/2026');
    });
  });
}
