import 'package:advocacia_mobile/core/mock/mock_auth_repository.dart';
import 'package:advocacia_mobile/core/providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

Future<void> fill(WidgetTester tester, {String email = '', String password = ''}) async {
  await tester.enterText(find.widgetWithText(TextFormField, 'E-mail'), email);
  await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), password);
}

void main() {
  testWidgets('mostra os campos e o botão Entrar', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false));

    expect(find.widgetWithText(TextFormField, 'E-mail'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Senha'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Entrar'), findsOneWidget);
    expect(find.text('Entre para acompanhar seus processos.'), findsOneWidget);
  });

  testWidgets('sem sessão guardada, NÃO oferece biometria (não há o que desbloquear)', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false, biometricAvailable: true));

    expect(find.textContaining('biometria'), findsNothing);
  });

  testWidgets('valida os campos antes de enviar', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false));

    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pump();

    expect(find.text('Informe seu e-mail.'), findsOneWidget);
    expect(find.text('Informe sua senha.'), findsOneWidget);

    await fill(tester, email: 'sem-arroba', password: 'x');
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pump();
    expect(find.text('E-mail inválido.'), findsOneWidget);
  });

  testWidgets('credencial errada mostra o erro e continua no login', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false));

    await fill(tester, email: 'ana.souza@escritorio.com.br', password: 'errada');
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('E-mail ou senha incorretos.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Entrar'), findsOneWidget);
  });

  testWidgets('login válido leva ao painel', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false));

    await fill(
      tester,
      email: MockAuthRepository.demoEmail,
      password: MockAuthRepository.demoPassword,
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Processos ativos'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Entrar'), findsNothing);
  });

  testWidgets('o botão de senha alterna a visibilidade', (tester) async {
    await pumpApp(tester, await buildTestApp(signedIn: false));
    bool obscured() =>
        tester.widget<EditableText>(find.descendant(
          of: find.widgetWithText(TextFormField, 'Senha'),
          matching: find.byType(EditableText),
        )).obscureText;

    expect(obscured(), isTrue);
    await tester.tap(find.byTooltip('Mostrar senha'));
    await tester.pump();
    expect(obscured(), isFalse);
  });

  group('bloqueio biométrico (sessão guardada)', () {
    testWidgets('abre no login com o botão de biometria e destrava com ele', (tester) async {
      final app = await buildTestApp(signedIn: true, biometricLock: true);
      await pumpApp(tester, app);

      // Tem sessão, mas o conteúdo NÃO aparece: o app abriu bloqueado.
      expect(find.text('Processos ativos'), findsNothing);
      expect(find.text('Bem-vindo de volta, Ana Souza.'), findsOneWidget);

      await tester.tap(find.text('Entrar com Face ID / biometria'));
      await tester.pumpAndSettle();

      expect(app.biometrics.authenticateCalls, 1);
      expect(find.text('Processos ativos'), findsOneWidget);
    });

    testWidgets('biometria recusada mantém bloqueado e explica', (tester) async {
      final app = await buildTestApp(signedIn: true, biometricLock: true);
      app.biometrics.succeeds = false;
      await pumpApp(tester, app);

      await tester.tap(find.text('Entrar com Face ID / biometria'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Não foi possível confirmar sua identidade'), findsOneWidget);
      expect(find.text('Processos ativos'), findsNothing);
    });

    testWidgets('aparelho sem biometria: mostra só a senha', (tester) async {
      await pumpApp(
        tester,
        await buildTestApp(signedIn: true, biometricLock: true, biometricAvailable: false),
      );

      expect(find.text('Entrar com Face ID / biometria'), findsNothing);
      expect(find.widgetWithText(FilledButton, 'Entrar'), findsOneWidget);
    });

    testWidgets('"Entrar com outra conta" encerra a sessão guardada', (tester) async {
      final container = await pumpApp(tester, await buildTestApp(signedIn: true, biometricLock: true));

      await tester.tap(find.text('Entrar com outra conta'));
      await tester.pumpAndSettle();

      expect(container.read(authControllerProvider).hasSession, isFalse);
      expect(find.text('Entre para acompanhar seus processos.'), findsOneWidget);
    });
  });
}
