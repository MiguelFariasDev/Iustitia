import 'package:advocacia_mobile/features/dashboard/data/dashboard_repository.dart';
import 'package:advocacia_mobile/features/dashboard/domain/dashboard_data.dart';
import 'package:advocacia_mobile/core/errors/app_exception.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

class _CountingRepository implements DashboardRepository {
  int loads = 0;
  bool failNext = false;

  @override
  Future<DashboardData> load() async {
    loads++;
    if (failNext) throw ApiException(status: 0);
    return const DashboardData(
      activeProcesses: 7,
      unreadPublications: 3,
      pendingReview: 3,
      recentPublications: [],
    );
  }
}

void main() {
  testWidgets('saudação por hora e nome, sem presumir título de gênero', (tester) async {
    await pumpApp(tester, await buildTestApp()); // relógio fixo: 9h

    expect(find.text('Bom dia, Dr(a). Ana'), findsOneWidget);
    expect(find.text('Terça-feira, 10 de novembro'), findsOneWidget);
  });

  testWidgets('mostra os cartões de resumo com dados reais', (tester) async {
    await pumpApp(tester, await buildTestApp());

    Finder valueOf(String label) => find.descendant(
          of: find.ancestor(of: find.text(label), matching: find.byType(Card)),
          matching: find.text('2'),
        );

    expect(valueOf('Processos ativos'), findsOneWidget);
    expect(valueOf('Publicações não lidas'), findsOneWidget);
  });

  testWidgets('"Próximos prazos" não tem número: prazos ainda não existem no backend', (tester) async {
    await pumpApp(tester, await buildTestApp());

    expect(find.text('Próximos prazos'), findsOneWidget);
    expect(find.text('Em breve'), findsOneWidget);
  });

  testWidgets('cartões são botões e levam à aba correspondente', (tester) async {
    await pumpApp(tester, await buildTestApp());

    await tester.tap(find.text('Publicações não lidas'));
    await tester.pumpAndSettle();
    expect(find.text('Buscar por CNJ, cliente ou parte'), findsOneWidget);
  });

  testWidgets('cartão de prazos leva à agenda', (tester) async {
    await pumpApp(tester, await buildTestApp());

    await tester.tap(find.text('Próximos prazos'));
    await tester.pumpAndSettle();
    expect(find.text('Mês'), findsOneWidget); // controle de visão da agenda
  });

  testWidgets('últimas movimentações: publicações com cliente, tribunal e trecho', (tester) async {
    await pumpApp(tester, await buildTestApp());

    expect(find.text('Últimas movimentações'), findsOneWidget);
    expect(find.text('Maria Aparecida Silva'), findsOneWidget);
    expect(find.textContaining('Intimação para apresentar réplica'), findsOneWidget);
  });

  testWidgets('tocar numa movimentação abre o processo dela', (tester) async {
    await pumpApp(tester, await buildTestApp());

    await tester.tap(find.text('Maria Aparecida Silva'));
    await tester.pumpAndSettle();

    expect(find.text('Dados'), findsOneWidget);
    expect(find.text('5001001-10.2026.8.26.0001'), findsWidgets);
  });

  testWidgets('o selo de alertas não lidos aparece no ícone do Painel', (tester) async {
    await pumpApp(tester, await buildTestApp()); // MockAlertsRepository: 2 não lidos

    expect(
      find.descendant(of: find.byType(NavigationBar), matching: find.byType(Badge)),
      findsOneWidget,
    );
    expect(find.descendant(of: find.byType(Badge), matching: find.text('2')), findsOneWidget);
  });

  testWidgets('pull-to-refresh recarrega o painel', (tester) async {
    final repo = _CountingRepository();
    await pumpApp(
      tester,
      await buildTestApp(dashboard: repo),
    );
    expect(repo.loads, 1);
    expect(find.text('7'), findsOneWidget);

    await tester.fling(find.byType(ListView).first, const Offset(0, 400), 1000);
    await tester.pumpAndSettle();

    expect(repo.loads, 2);
  });

  testWidgets('erro de carregamento oferece "Tentar novamente" e recupera', (tester) async {
    final repo = _CountingRepository()..failNext = true;
    await pumpApp(
      tester,
      await buildTestApp(dashboard: repo),
    );

    expect(find.text('Não foi possível carregar'), findsOneWidget);
    expect(find.textContaining('conectar ao servidor'), findsOneWidget);

    repo.failNext = false;
    await tester.tap(find.text('Tentar novamente'));
    await tester.pumpAndSettle();

    expect(find.text('Processos ativos'), findsOneWidget);
    expect(find.text('7'), findsOneWidget);
  });

  testWidgets('os cartões têm semântica de botão com rótulo e valor', (tester) async {
    final handle = tester.ensureSemantics();
    await pumpApp(tester, await buildTestApp());

    expect(find.bySemanticsLabel('Processos ativos: 2'), findsOneWidget);
    expect(
      find.bySemanticsLabel(RegExp('Próximos prazos: Em breve')),
      findsOneWidget,
    );
    handle.dispose();
  });
}
