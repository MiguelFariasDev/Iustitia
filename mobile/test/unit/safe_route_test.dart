import 'package:advocacia_mobile/core/utils/safe_route.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('safeInternalRoute aceita só rotas internas conhecidas', () {
    for (final route in [
      '/dashboard',
      '/processos',
      '/processos/9f1c2d3e-0000-4000-8000-000000000001',
      '/clientes/cli-001',
      '/agenda',
      '/perfil',
    ]) {
      test(route, () => expect(safeInternalRoute(route), route));
    }

    test('normaliza ".." para um destino que também é permitido', () {
      // O Uri do Dart resolve os segmentos de ponto: o resultado é uma rota da lista, nunca
      // algo fora dela — por isso é seguro devolvê-lo normalizado em vez de rejeitar.
      expect(safeInternalRoute('/processos/../clientes'), '/clientes');
      expect(safeInternalRoute('/processos/../../login'), isNull);
    });

    test('tira espaços e descarta a query string', () {
      expect(safeInternalRoute('  /processos/abc?x=1 '), '/processos/abc');
    });
  });

  group('rejeita o que poderia levar para fora ou para onde não deve', () {
    for (final route in [
      null,
      '',
      'processos',
      '//evil.com',
      'https://evil.com/processos',
      'javascript:alert(1)',
      r'/\evil.com',
      '/login',
      '/processos/a/b',
      '/processos/%2e%2e',
      '/configuracoes',
    ]) {
      test('$route', () => expect(safeInternalRoute(route), isNull));
    }
  });
}
