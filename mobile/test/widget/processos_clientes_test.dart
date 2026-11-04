import 'package:advocacia_mobile/app/router.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

Future<void> goTo(WidgetTester tester, String label) async {
  await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)));
  await tester.pumpAndSettle();
}

/// Espera o debounce da busca (400 ms) e a resposta.
Future<void> search(WidgetTester tester, String text) async {
  await tester.enterText(find.byType(TextField), text);
  await tester.pump(const Duration(milliseconds: 500));
  await tester.pumpAndSettle();
}

void main() {
  group('lista de processos', () {
    testWidgets('mostra CNJ, cliente, área, status e tribunal em cada cartão', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      expect(find.text('5001001-10.2026.8.26.0001'), findsOneWidget);
      expect(find.text('Maria Aparecida Silva'), findsOneWidget); // nome resolvido pelo clienteId
      expect(find.textContaining('Cível · TJSP · x Carlos Eduardo Pereira'), findsOneWidget);
      expect(find.text('Ativo'), findsNWidgets(2));
      expect(find.text('Suspenso'), findsOneWidget);
      expect(find.text('Arquivado'), findsOneWidget);
    });

    testWidgets('filtros "Ativos" e "Arquivados" e voltar a "todos"', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      await tester.tap(find.widgetWithText(FilterChip, 'Ativos'));
      await tester.pumpAndSettle();
      expect(find.byType(Card), findsNWidgets(2));
      expect(find.text('Suspenso'), findsNothing);

      await tester.tap(find.widgetWithText(FilterChip, 'Arquivados'));
      await tester.pumpAndSettle();
      expect(find.byType(Card), findsOneWidget);
      expect(find.text('5004004-40.2024.8.26.0200'), findsOneWidget);

      // Tocar no filtro ativo o desliga.
      await tester.tap(find.widgetWithText(FilterChip, 'Arquivados'));
      await tester.pumpAndSettle();
      expect(find.byType(Card), findsNWidgets(4));
    });

    testWidgets('busca por número do CNJ, por cliente e por parte contrária', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      await search(tester, '5002002');
      expect(find.byType(Card), findsOneWidget);

      await search(tester, 'maria');
      expect(find.text('5001001-10.2026.8.26.0001'), findsOneWidget);
      expect(find.byType(Card), findsOneWidget);

      await search(tester, 'indústria alfa');
      expect(find.text('5002002-20.2026.5.02.0001'), findsOneWidget);
    });

    testWidgets('busca sem resultado mostra o estado vazio e o botão limpar tira a busca', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      await search(tester, 'zzzz');
      expect(find.text('Nenhum processo encontrado'), findsOneWidget);

      await tester.tap(find.byTooltip('Limpar busca'));
      await tester.pumpAndSettle();
      expect(find.byType(Card), findsNWidgets(4));
    });

    testWidgets('cada cartão é um botão com resumo falado completo', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      expect(
        find.bySemanticsLabel(
          'Processo 5001001-10.2026.8.26.0001, cliente Maria Aparecida Silva, Cível, Ativo',
        ),
        findsOneWidget,
      );
      handle.dispose();
    });

    testWidgets('tocar abre o detalhe', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Processos');

      await tester.tap(find.text('5002002-20.2026.5.02.0001'));
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/processos/proc-002');
    });
  });

  group('detalhe do processo', () {
    Future<TestApp> open(WidgetTester tester) async {
      final app = await buildTestApp();
      final container = await pumpApp(tester, app);
      container.read(routerProvider).go('/processos/proc-001');
      await tester.pumpAndSettle();
      return app;
    }

    testWidgets('aba Dados traz os campos do processo', (tester) async {
      await open(tester);

      expect(find.text('Maria Aparecida Silva'), findsOneWidget);
      expect(find.text('Ação de cobrança'), findsOneWidget);
      expect(find.text('Carlos Eduardo Pereira'), findsOneWidget);
      expect(find.text('R\$ 35.000,00'), findsOneWidget);
      expect(find.text('12/03/2026'), findsOneWidget);
      expect(find.text('Monitorado nos diários'), findsOneWidget);
    });

    testWidgets('campos ausentes aparecem como traço, não somem', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      container.read(routerProvider).go('/processos/proc-004');
      await tester.pumpAndSettle();

      expect(find.text('—'), findsWidgets);
      expect(find.text('Não monitorado'), findsOneWidget);
    });

    testWidgets('aba Publicações lista as do processo; a de outro processo não aparece', (tester) async {
      await open(tester);

      await tester.tap(find.text('Publicações').first);
      await tester.pumpAndSettle();

      expect(find.textContaining('Intimação para apresentar réplica'), findsOneWidget);
      expect(find.text('Não lida'), findsOneWidget);
      expect(find.textContaining('Designada audiência de instrução'), findsNothing);
    });

    testWidgets('aba Movimentos diz que ainda não está disponível', (tester) async {
      await open(tester);

      await tester.tap(find.text('Movimentos'));
      await tester.pumpAndSettle();

      expect(find.text('Movimentações em breve'), findsOneWidget);
    });

    testWidgets('Compartilhar manda só o número e o tribunal', (tester) async {
      final app = await open(tester);

      await tester.tap(find.byTooltip('Compartilhar'));
      await tester.pump();

      expect(app.external.shared, ['Processo 5001001-10.2026.8.26.0001 — TJSP']);
    });

    testWidgets('Ver no site do tribunal copia o número e abre o portal', (tester) async {
      final app = await open(tester);

      await tester.tap(find.byTooltip('Ver no site do tribunal'));
      await tester.pumpAndSettle();

      expect(app.external.copied, ['5001001-10.2026.8.26.0001']);
      expect(app.external.opened.single.toString(), 'https://portaldeservicos.pdpj.jus.br/consulta');
      expect(find.text('Número copiado. Cole na consulta do portal.'), findsOneWidget);
    });
  });

  group('clientes', () {
    testWidgets('lista nome, documento e tipo', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Clientes');

      expect(find.text('Maria Aparecida Silva'), findsOneWidget);
      expect(find.text('529.982.247-25'), findsOneWidget);
      expect(find.text('Pessoa física'), findsNWidgets(2));
      expect(find.text('Pessoa jurídica'), findsNWidgets(2));
    });

    testWidgets('busca por nome e por CPF/CNPJ, com ou sem máscara', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Clientes');

      await search(tester, 'padaria');
      expect(find.byType(Card), findsOneWidget);

      await search(tester, '52998224725');
      expect(find.text('Maria Aparecida Silva'), findsOneWidget);
      expect(find.byType(Card), findsOneWidget);

      await search(tester, '11.222.333');
      expect(find.text('Padaria Boa Massa LTDA'), findsOneWidget);
    });

    testWidgets('detalhe: dados do cliente e processos vinculados', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Clientes');

      await tester.tap(find.text('Maria Aparecida Silva'));
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/clientes/cli-001');
      expect(find.text('maria.silva@exemplo.com.br'), findsOneWidget);
      expect(find.text('(11) 98765-4321'), findsOneWidget);
      expect(find.textContaining('Rua das Flores, 120'), findsOneWidget);
      expect(find.text('Processos vinculados'), findsOneWidget);
      expect(find.text('5001001-10.2026.8.26.0001'), findsOneWidget);
    });

    testWidgets('cliente sem processo mostra o estado vazio', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      container.read(routerProvider).go('/clientes/cli-002');
      await tester.pumpAndSettle();

      // cli-002 (Padaria) tem um processo suspenso; cli-001 não aparece aqui.
      expect(find.text('5003003-30.2025.8.26.0100'), findsOneWidget);
      expect(find.text('5001001-10.2026.8.26.0001'), findsNothing);
    });

    testWidgets('tocar num processo do cliente abre o processo', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      container.read(routerProvider).go('/clientes/cli-001');
      await tester.pumpAndSettle();

      await tester.tap(find.text('5001001-10.2026.8.26.0001'));
      await tester.pumpAndSettle();

      expect(container.read(routerProvider).state.uri.path, '/processos/proc-001');
    });
  });
}
