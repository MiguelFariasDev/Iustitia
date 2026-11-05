import 'package:advocacia_mobile/core/auth/auth_repository.dart';
import 'package:advocacia_mobile/core/mock/mock_auth_repository.dart';
import 'package:advocacia_mobile/core/providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

Future<ProviderContainer> containerFor({
  required bool signedIn,
  bool lock = false,
  bool biometricsAvailable = true,
  bool biometricsSucceed = true,
}) async {
  final app = await buildTestApp(
    signedIn: signedIn,
    biometricLock: lock,
    biometricAvailable: biometricsAvailable,
  );
  app.biometrics.succeeds = biometricsSucceed;

  final container = ProviderContainer(overrides: app.overrides);
  addTearDown(container.dispose);
  return container;
}

void main() {
  group('abertura do app', () {
    test('sem sessão: não está logado', () async {
      final c = await containerFor(signedIn: false);

      expect(c.read(authControllerProvider).isSignedIn, isFalse);
      expect(c.read(authControllerProvider).hasSession, isFalse);
    });

    test('com sessão e sem bloqueio: entra direto', () async {
      final c = await containerFor(signedIn: true);

      expect(c.read(authControllerProvider).isSignedIn, isTrue);
    });

    test('com sessão E bloqueio biométrico: tem sessão mas NÃO libera o conteúdo', () async {
      final c = await containerFor(signedIn: true, lock: true);
      final state = c.read(authControllerProvider);

      expect(state.hasSession, isTrue);
      expect(state.locked, isTrue);
      expect(state.isSignedIn, isFalse);
    });
  });

  group('desbloqueio', () {
    test('biometria aprovada libera', () async {
      final c = await containerFor(signedIn: true, lock: true);

      final ok = await c.read(authControllerProvider.notifier).unlockWithBiometrics();

      expect(ok, isTrue);
      expect(c.read(authControllerProvider).isSignedIn, isTrue);
    });

    test('biometria recusada mantém bloqueado', () async {
      final c = await containerFor(signedIn: true, lock: true, biometricsSucceed: false);

      final ok = await c.read(authControllerProvider.notifier).unlockWithBiometrics();

      expect(ok, isFalse);
      expect(c.read(authControllerProvider).isSignedIn, isFalse);
    });

    test('sem sessão não há o que desbloquear', () async {
      final c = await containerFor(signedIn: false);

      expect(await c.read(authControllerProvider.notifier).unlockWithBiometrics(), isFalse);
    });
  });

  group('login e logout', () {
    test('entrar com a senha libera mesmo com bloqueio ligado', () async {
      final c = await containerFor(signedIn: false, lock: true);

      await c.read(authControllerProvider.notifier).signIn(
            email: MockAuthRepository.demoEmail,
            password: MockAuthRepository.demoPassword,
          );

      expect(c.read(authControllerProvider).isSignedIn, isTrue);
    });

    test('credencial errada lança AuthFailure e não loga', () async {
      final c = await containerFor(signedIn: false);

      await expectLater(
        c.read(authControllerProvider.notifier).signIn(email: 'x@y.com', password: 'errada'),
        throwsA(isA<AuthFailure>().having((e) => e.message, 'message', 'E-mail ou senha incorretos.')),
      );
      expect(c.read(authControllerProvider).hasSession, isFalse);
    });

    test('sair esquece a sessão e o bloqueio', () async {
      final c = await containerFor(signedIn: true, lock: true);

      await c.read(authControllerProvider.notifier).signOut();

      expect(c.read(authControllerProvider).hasSession, isFalse);
      expect(c.read(authControllerProvider).locked, isFalse);
    });

    test('sessão expirada (401 após renovar) derruba o usuário', () async {
      final c = await containerFor(signedIn: true);

      c.read(authControllerProvider.notifier).sessionExpired();

      expect(c.read(authControllerProvider).isSignedIn, isFalse);
    });
  });

  group('ligar o bloqueio por biometria', () {
    test('exige biometria disponível e confirmada', () async {
      final c = await containerFor(signedIn: true);

      expect(await c.read(authControllerProvider.notifier).setBiometricLock(true), isTrue);
      expect(c.read(settingsStoreProvider).biometricLock, isTrue);
    });

    test('aparelho sem biometria: não liga (senão ninguém passaria pelo bloqueio)', () async {
      final c = await containerFor(signedIn: true, biometricsAvailable: false);

      expect(await c.read(authControllerProvider.notifier).setBiometricLock(true), isFalse);
      expect(c.read(settingsStoreProvider).biometricLock, isFalse);
    });

    test('confirmação recusada: não liga', () async {
      final c = await containerFor(signedIn: true, biometricsSucceed: false);

      expect(await c.read(authControllerProvider.notifier).setBiometricLock(true), isFalse);
      expect(c.read(settingsStoreProvider).biometricLock, isFalse);
    });

    test('desligar não pede biometria', () async {
      final c = await containerFor(signedIn: true, lock: true, biometricsSucceed: false);

      expect(await c.read(authControllerProvider.notifier).setBiometricLock(false), isTrue);
      expect(c.read(settingsStoreProvider).biometricLock, isFalse);
    });
  });
}
