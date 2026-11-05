import 'package:advocacia_mobile/app/router.dart';
import 'package:advocacia_mobile/core/providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

void main() {
  group('redirecionamento global', () {
    testWidgets('sem sessão, qualquer rota cai no login', (tester) async {
      final container = await pumpApp(tester, await buildTestApp(signedIn: false));
      final router = container.read(routerProvider);

      router.go('/processos');
      await tester.pumpAndSettle();

      expect(find.text('Entre para acompanhar seus processos.'), findsOneWidget);
      expect(router.state.uri.path, '/login');
    });

    testWidgets('com sessão, o app abre no painel', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());

      expect(container.read(routerProvider).state.uri.path, '/dashboard');
      expect(find.text('Processos ativos'), findsOneWidget);
    });

    testWidgets('logado, /login volta para o painel', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());

      container.read(routerProvider).go('/login');
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/dashboard');
    });

    testWidgets('a raiz "/" redireciona para o painel', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());

      container.read(routerProvider).go('/');
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/dashboard');
    });

    testWidgets('logout leva ao login', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());

      await container.read(authControllerProvider.notifier).signOut();
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/login');
    });

    testWidgets('abre o destino pedido depois do login (ex.: veio de uma notificação)', (tester) async {
      final container = await pumpApp(tester, await buildTestApp(signedIn: false));
      final router = container.read(routerProvider);

      router.go('/processos/proc-002');
      await tester.pumpAndSettle();
      expect(router.state.uri.path, '/login');
      expect(router.state.uri.queryParameters['from'], '/processos/proc-002');

      await tester.enterText(find.widgetWithText(TextFormField, 'E-mail'), 'ana.souza@escritorio.com.br');
      await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), 'demo1234');
      await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
      await tester.pumpAndSettle();

      expect(router.state.uri.path, '/processos/proc-002');
      expect(find.text('5002002-20.2026.5.02.0001'), findsWidgets);
    });

    testWidgets('um "from" malicioso é ignorado: vai para o painel', (tester) async {
      final container = await pumpApp(tester, await buildTestApp(signedIn: false));
      final router = container.read(routerProvider);

      router.go('/login?from=${Uri.encodeQueryComponent('https://evil.com')}');
      await tester.pumpAndSettle();
      await container.read(authControllerProvider.notifier).signIn(
            email: 'ana.souza@escritorio.com.br',
            password: 'demo1234',
          );
      await tester.pumpAndSettle();

      expect(router.state.uri.path, '/dashboard');
    });
  });

  group('barra de navegação inferior', () {
    testWidgets('tem as cinco abas e troca entre elas', (tester) async {
      await pumpApp(tester, await buildTestApp());

      for (final label in ['Painel', 'Processos', 'Clientes', 'Agenda', 'Perfil']) {
        expect(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)), findsOneWidget);
      }

      Future<void> open(String label) async {
        await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)));
        await tester.pumpAndSettle();
      }

      await open('Processos');
      expect(find.text('Buscar por CNJ, cliente ou parte'), findsOneWidget);
      await open('Clientes');
      expect(find.text('Buscar por nome ou CPF/CNPJ'), findsOneWidget);
      await open('Agenda');
      expect(find.text('Semana'), findsOneWidget);
      await open('Perfil');
      expect(find.text('Sair'), findsOneWidget);
      await open('Painel');
      expect(find.text('Processos ativos'), findsOneWidget);
    });

    testWidgets('cada aba guarda a própria pilha ao trocar', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      final router = container.read(routerProvider);

      router.go('/processos/proc-001');
      await tester.pumpAndSettle();
      expect(find.text('Dados'), findsOneWidget);

      await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Clientes')));
      await tester.pumpAndSettle();
      await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Processos')));
      await tester.pumpAndSettle();

      // Voltou para o detalhe em que estava, não para a lista.
      expect(find.text('Dados'), findsOneWidget);
    });

    testWidgets('tocar na aba ativa volta à raiz dela', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      container.read(routerProvider).go('/processos/proc-001');
      await tester.pumpAndSettle();

      await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Processos')));
      await tester.pumpAndSettle();

      expect(find.text('Buscar por CNJ, cliente ou parte'), findsOneWidget);
    });
  });

  group('rotas aninhadas', () {
    testWidgets('/processos/:id e /clientes/:id abrem o detalhe', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      final router = container.read(routerProvider);

      router.go('/processos/proc-001');
      await tester.pumpAndSettle();
      expect(find.text('Publicações'), findsOneWidget); // aba do detalhe

      router.go('/clientes/cli-001');
      await tester.pumpAndSettle();
      expect(find.text('Processos vinculados'), findsOneWidget);
    });

    testWidgets('processo inexistente mostra erro com "Tentar novamente"', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());

      container.read(routerProvider).go('/processos/nao-existe');
      await tester.pumpAndSettle();

      expect(find.text('Não foi possível carregar'), findsOneWidget);
      expect(find.text('Recurso não encontrado.'), findsOneWidget);
    });
  });
}
