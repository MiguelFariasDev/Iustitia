import 'package:advocacia_mobile/app/router.dart';
import 'package:advocacia_mobile/core/providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

Future<void> goTo(WidgetTester tester, String label) async {
  await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)));
  await tester.pumpAndSettle();
}

// Relógio fixo dos testes: terça-feira, 10/11/2026. Eventos de demonstração:
// 08/11 audiência (09:30) · 11/11 prazo (18:00) · 13/11 reunião (10:00) e audiência (14:00)
// · 18/11 prazo (18:00).
/// Rola a lista da agenda até o evento aparecer (a ListView constrói só o que está perto da tela).
Future<void> reveal(WidgetTester tester, String text) => tester.scrollUntilVisible(
      find.text(text),
      200,
      scrollable: find.byType(Scrollable).first,
    );

Finder day(String label) => find.bySemanticsLabel(RegExp('^$label'));

void main() {
  group('agenda', () {
    testWidgets('avisa que os eventos são de demonstração', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      expect(find.text('Dados de demonstração. '), findsNothing); // é um Text.rich
      expect(find.textContaining('Dados de demonstração'), findsOneWidget);
      expect(find.textContaining('nada aqui é do seu escritório'), findsOneWidget);
    });

    testWidgets('abre no mês e no dia de hoje, sem eventos neste dia', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      expect(find.text('Novembro de 2026'), findsOneWidget);
      expect(find.text('Terça-feira, 10 de novembro'), findsOneWidget);
      expect(find.text('Nada neste dia'), findsOneWidget);
    });

    testWidgets('tocar num dia lista os eventos dele, em ordem de horário', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      await tester.tap(day('Sexta-feira, 13 de novembro, 2 eventos'));
      await tester.pumpAndSettle();

      expect(find.text('Sexta-feira, 13 de novembro'), findsOneWidget);
      await reveal(tester, 'Audiência de conciliação');
      final reuniao = tester.getTopLeft(find.text('Reunião com cliente'));
      final audiencia = tester.getTopLeft(find.text('Audiência de conciliação'));
      expect(reuniao.dy, lessThan(audiencia.dy)); // 10:00 antes de 14:00
      handle.dispose();
    });

    testWidgets('cada evento diz o tipo por texto, não só por cor', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      await tester.tap(day('Quarta-feira, 11 de novembro, 1 evento'));
      await tester.pumpAndSettle();

      expect(find.text('Prazo'), findsOneWidget); // selo com o nome do tipo
      expect(find.bySemanticsLabel('Prazo, Prazo: contestação, 18:00'), findsOneWidget);
      handle.dispose();
    });

    testWidgets('tocar no evento abre os detalhes e "Abrir processo" leva ao processo', (tester) async {
      final handle = tester.ensureSemantics();
      final container = await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');
      await tester.tap(day('Sexta-feira, 13 de novembro, 2 eventos'));
      await tester.pumpAndSettle();

      await reveal(tester, 'Audiência de conciliação');
      await tester.tap(find.text('Audiência de conciliação'));
      await tester.pumpAndSettle();

      // O local também aparece no cartão da lista: procura só dentro da folha de detalhes.
      Finder inSheet(String text) =>
          find.descendant(of: find.byType(BottomSheet), matching: find.text(text));
      expect(inSheet('13/11/2026 às 14:00'), findsOneWidget);
      expect(inSheet('Fórum João Mendes, sala 12'), findsOneWidget);

      await tester.tap(find.text('Abrir processo'));
      await tester.pumpAndSettle();
      expect(container.read(routerProvider).state.uri.path, '/processos/proc-002');
      handle.dispose();
    });

    testWidgets('evento sem processo (reunião) não oferece "Abrir processo"', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');
      await tester.tap(day('Sexta-feira, 13 de novembro, 2 eventos'));
      await tester.pumpAndSettle();

      await reveal(tester, 'Reunião com cliente');
      await tester.tap(find.text('Reunião com cliente'));
      await tester.pumpAndSettle();

      expect(
        find.descendant(of: find.byType(BottomSheet), matching: find.text('Escritório')),
        findsOneWidget,
      );
      expect(find.text('Abrir processo'), findsNothing);
      handle.dispose();
    });

    testWidgets('navega entre meses', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      await tester.tap(find.byTooltip('Próximo mês'));
      await tester.pumpAndSettle();
      expect(find.text('Dezembro de 2026'), findsOneWidget);

      await tester.tap(find.byTooltip('Mês anterior'));
      await tester.tap(find.byTooltip('Mês anterior'));
      await tester.pumpAndSettle();
      expect(find.text('Outubro de 2026'), findsOneWidget);
    });

    testWidgets('visão semanal mostra só os sete dias da semana e navega por semana', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      await tester.tap(find.text('Semana'));
      await tester.pumpAndSettle();

      // Semana de 08 a 14/11: o dia 13 tem 2 eventos, o dia 18 não está na tela.
      expect(day('Sexta-feira, 13 de novembro, 2 eventos'), findsOneWidget);
      expect(day('Quarta-feira, 18 de novembro'), findsNothing);

      await tester.tap(find.byTooltip('Próxima semana'));
      await tester.pumpAndSettle();
      expect(day('Quarta-feira, 18 de novembro, 1 evento'), findsOneWidget);
      handle.dispose();
    });

    testWidgets('o dia de hoje é anunciado como "hoje"', (tester) async {
      final handle = tester.ensureSemantics();
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Agenda');

      expect(day('Terça-feira, 10 de novembro, hoje, sem eventos'), findsOneWidget);
      handle.dispose();
    });
  });

  group('perfil', () {
    testWidgets('mostra nome, e-mail, OAB e papel do usuário', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Perfil');

      expect(find.text('Ana Souza'), findsOneWidget);
      expect(find.text('AS'), findsOneWidget);
      expect(find.text('ana.souza@escritorio.com.br'), findsOneWidget);
      expect(find.text('OAB/SP 123.456'), findsOneWidget);
      expect(find.text('Sócio'), findsOneWidget);
    });

    testWidgets('escolher o tema aplica e persiste a preferência', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Perfil');
      expect(container.read(themeModeProvider), ThemeMode.system); // padrão: segue o sistema

      await tester.tap(find.text('Escuro'));
      await tester.pumpAndSettle();

      expect(container.read(themeModeProvider), ThemeMode.dark);
      expect(container.read(settingsStoreProvider).themeMode, ThemeMode.dark);
      expect(Theme.of(tester.element(find.byType(Scaffold).first)).brightness, Brightness.dark);
    });

    testWidgets('o tema salvo é restaurado na abertura', (tester) async {
      final container = await pumpApp(tester, await buildTestApp(prefs: {'settings.theme_mode': 'light'}));

      expect(container.read(themeModeProvider), ThemeMode.light);
    });

    testWidgets('ligar o bloqueio exige biometria e fica salvo', (tester) async {
      final app = await buildTestApp();
      final container = await pumpApp(tester, app);
      await goTo(tester, 'Perfil');

      await tester.tap(find.byType(Switch).first);
      await tester.pumpAndSettle();

      expect(app.biometrics.authenticateCalls, 1);
      expect(container.read(settingsStoreProvider).biometricLock, isTrue);
    });

    testWidgets('sem biometria no aparelho o interruptor fica desabilitado', (tester) async {
      await pumpApp(tester, await buildTestApp(biometricAvailable: false));
      await goTo(tester, 'Perfil');

      expect(tester.widget<Switch>(find.byType(Switch).first).onChanged, isNull);
      expect(find.textContaining('Cadastre uma biometria'), findsOneWidget);
    });

    testWidgets('lembretes de prazo ficam desabilitados enquanto a agenda é de demonstração', (tester) async {
      await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Perfil');

      expect(tester.widget<Switch>(find.byType(Switch).last).onChanged, isNull);
      expect(find.textContaining('Disponível quando a agenda estiver ligada'), findsOneWidget);
    });

    testWidgets('Sair encerra a sessão e volta ao login', (tester) async {
      final container = await pumpApp(tester, await buildTestApp());
      await goTo(tester, 'Perfil');

      await tester.ensureVisible(find.text('Sair'));
      await tester.tap(find.text('Sair'));
      await tester.pumpAndSettle();

      expect(container.read(authControllerProvider).hasSession, isFalse);
      expect(container.read(routerProvider).state.uri.path, '/login');
    });
  });
}
