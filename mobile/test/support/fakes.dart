import 'dart:convert';

import 'package:advocacia_mobile/app/app.dart';
import 'package:advocacia_mobile/core/auth/biometric_service.dart';
import 'package:advocacia_mobile/core/mock/mock_auth_repository.dart';
import 'package:advocacia_mobile/core/mock/mock_repositories.dart';
import 'package:advocacia_mobile/core/notifications/alerts_realtime.dart';
import 'package:advocacia_mobile/core/notifications/local_notifications.dart';
import 'package:advocacia_mobile/core/providers.dart';
import 'package:advocacia_mobile/core/utils/external_actions.dart';
import 'package:advocacia_mobile/features/dashboard/data/dashboard_repository.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Biometria controlável pelo teste.
class FakeBiometricService implements BiometricService {
  FakeBiometricService({this.available = true, this.succeeds = true});

  bool available;
  bool succeeds;
  int authenticateCalls = 0;

  @override
  Future<bool> isAvailable() async => available;

  @override
  Future<bool> authenticate(String reason) async {
    authenticateCalls++;
    return succeeds;
  }
}

class FakeLocalNotifications implements LocalNotifications {
  final scheduled = <({int id, DateTime when, String title, String? payload})>[];
  int cancelAllCalls = 0;
  void Function(String? payload)? onTap;

  @override
  Future<void> init({required void Function(String? payload) onTap}) async => this.onTap = onTap;

  @override
  Future<bool> requestPermission() async => true;

  @override
  Future<void> show({required int id, required String title, String? body, String? payload}) async {}

  @override
  Future<void> scheduleAt({
    required int id,
    required DateTime when,
    required String title,
    String? body,
    String? payload,
  }) async =>
      scheduled.add((id: id, when: when, title: title, payload: payload));

  @override
  Future<void> cancelAll() async {
    cancelAllCalls++;
    scheduled.clear();
  }
}

class FakeExternalActions implements ExternalActions {
  final shared = <String>[];
  final opened = <Uri>[];
  final copied = <String>[];

  @override
  Future<void> share({required String text, String? subject}) async => shared.add(text);

  @override
  Future<bool> openUrl(Uri uri) async {
    opened.add(uri);
    return true;
  }

  @override
  Future<void> copy(String text) async => copied.add(text);
}

/// Adaptador do Dio que responde com o que o teste mandar e registra as requisições.
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.handler);

  final ResponseBody Function(RequestOptions options) handler;
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return handler(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody jsonResponse(Object body, {int status = 200}) => ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

/// Sessão inicial do app nos testes de widget.
class TestApp {
  TestApp({
    required this.overrides,
    required this.realtime,
    required this.biometrics,
    required this.external,
    required this.notifications,
  });

  final List<Override> overrides;
  final MockAlertsRealtime realtime;
  final FakeBiometricService biometrics;
  final FakeExternalActions external;
  final FakeLocalNotifications notifications;
}

Future<TestApp> buildTestApp({
  bool signedIn = true,
  bool biometricLock = false,
  bool biometricAvailable = true,
  Map<String, Object> prefs = const {},
  DashboardRepository? dashboard,
  List<Override> extra = const [],
}) async {
  SharedPreferences.setMockInitialValues({
    if (biometricLock) 'settings.biometric_lock': true,
    ...prefs,
  });
  final preferences = await SharedPreferences.getInstance();

  final realtime = MockAlertsRealtime();
  final biometrics = FakeBiometricService(available: biometricAvailable);
  final external = FakeExternalActions();
  final notifications = FakeLocalNotifications();

  return TestApp(
    realtime: realtime,
    biometrics: biometrics,
    external: external,
    notifications: notifications,
    overrides: [
      sharedPreferencesProvider.overrideWithValue(preferences),
      useGoogleFontsProvider.overrideWithValue(false),
      // Terça-feira, 10/11/2026, 9h: a agenda de demonstração fica previsível.
      clockProvider.overrideWithValue(() => DateTime(2026, 11, 10, 9)),
      authRepositoryProvider
          .overrideWithValue(MockAuthRepository(signedIn: signedIn ? MockAuthRepository.demoUser : null)),
      processoRepositoryProvider.overrideWithValue(MockProcessoRepository()),
      clienteRepositoryProvider.overrideWithValue(MockClienteRepository()),
      dashboardRepositoryProvider.overrideWithValue(dashboard ?? MockDashboardRepository()),
      alertsRepositoryProvider.overrideWithValue(MockAlertsRepository()),
      alertsRealtimeProvider.overrideWithValue(realtime),
      biometricServiceProvider.overrideWithValue(biometrics),
      externalActionsProvider.overrideWithValue(external),
      localNotificationsProvider.overrideWithValue(notifications),
      ...extra,
    ],
  );
}

Future<ProviderContainer> pumpApp(WidgetTester tester, TestApp app) async {
  // Tela de celular: o layout é pensado para ~390dp de largura.
  tester.view
    ..physicalSize = const Size(1170, 2532)
    ..devicePixelRatio = 3;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(ProviderScope(
    retry: (_, _) => null,
    overrides: app.overrides,
    child: const AdvocaciaApp(),
  ));
  await tester.pumpAndSettle();

  return ProviderScope.containerOf(tester.element(find.byType(AdvocaciaApp)));
}
