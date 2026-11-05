import 'package:advocacia_mobile/app/router.dart';
import 'package:advocacia_mobile/core/notifications/alerts_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

const publicationAlert = AlertPayload(
  alertId: 'a1',
  type: 'Publication',
  title: 'Nova publicação: intimação',
  description: 'Processo 5001001-10.2026.8.26.0001 — TJSP',
  referenceId: 'pub-001',
);

void main() {
  group('alerta em tempo real (Supabase Realtime)', () {
    testWidgets('mostra um banner no topo com título e descrição', (tester) async {
      final app = await buildTestApp();
      await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();

      expect(find.byType(MaterialBanner), findsOneWidget);
      expect(find.text('Nova publicação: intimação'), findsOneWidget);
      expect(find.text('Processo 5001001-10.2026.8.26.0001 — TJSP'), findsOneWidget);
    });

    testWidgets('"Ver" leva ao processo da publicação e fecha o banner', (tester) async {
      final app = await buildTestApp();
      final container = await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ver'));
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/processos/proc-001');
      expect(find.byType(MaterialBanner), findsNothing);
    });

    testWidgets('alerta sem destino mostra o banner, mas sem o botão "Ver"', (tester) async {
      final app = await buildTestApp();
      await pumpApp(tester, app);

      app.realtime.push(const AlertPayload(alertId: 'a2', type: 'Birthday', title: 'Aniversário de cliente'));
      await tester.pumpAndSettle();

      expect(find.text('Aniversário de cliente'), findsOneWidget);
      expect(find.text('Ver'), findsNothing);
      expect(find.text('Dispensar'), findsOneWidget);
    });

    testWidgets('"Dispensar" fecha o banner', (tester) async {
      final app = await buildTestApp();
      await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dispensar'));
      await tester.pumpAndSettle();

      expect(find.byType(MaterialBanner), findsNothing);
    });

    testWidgets('o banner some sozinho depois de alguns segundos', (tester) async {
      final app = await buildTestApp();
      await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();
      expect(find.byType(MaterialBanner), findsOneWidget);

      await tester.pump(const Duration(seconds: 9));
      await tester.pumpAndSettle();
      expect(find.byType(MaterialBanner), findsNothing);
    });

    testWidgets('um segundo alerta substitui o primeiro em vez de empilhar', (tester) async {
      final app = await buildTestApp();
      await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();
      app.realtime.push(const AlertPayload(alertId: 'a3', type: 'Task', title: 'Tarefa vence amanhã'));
      await tester.pumpAndSettle();

      expect(find.byType(MaterialBanner), findsOneWidget);
      expect(find.text('Tarefa vence amanhã'), findsOneWidget);
      expect(find.text('Nova publicação: intimação'), findsNothing);
    });

    testWidgets('sem sessão, o alerta não aparece (o canal nem é aberto)', (tester) async {
      final app = await buildTestApp(signedIn: false);
      await pumpApp(tester, app);

      app.realtime.push(publicationAlert);
      await tester.pumpAndSettle();

      expect(find.byType(MaterialBanner), findsNothing);
    });
  });

  group('toque em notificação (push e local) — deep link', () {
    testWidgets('abre a tela indicada pela rota da notificação', (tester) async {
      final app = await buildTestApp();
      final container = await pumpApp(tester, app);
      await tester.pump(); // o host inicia o push depois do primeiro frame

      app.notifications.onTap!('/processos/proc-002');
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/processos/proc-002');
    });

    testWidgets('rota de fora do app é ignorada (não navega)', (tester) async {
      final app = await buildTestApp();
      final container = await pumpApp(tester, app);
      await tester.pump();

      for (final hostile in ['https://evil.com', '//evil.com', '/login', '/processos/a/b', null]) {
        app.notifications.onTap!(hostile);
        await tester.pumpAndSettle();
        expect(container.read(routerProvider).state.uri.path, '/dashboard', reason: '$hostile');
      }
    });
  });
}
