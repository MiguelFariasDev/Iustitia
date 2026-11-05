import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import '../../app/config.dart';
import '../api/api_client.dart';
import '../errors/app_exception.dart';
import '../utils/safe_route.dart';
import 'local_notifications.dart';

FirebaseOptions get _firebaseOptions => const FirebaseOptions(
      apiKey: AppConfig.firebaseApiKey,
      appId: AppConfig.firebaseAppId,
      messagingSenderId: AppConfig.firebaseSenderId,
      projectId: AppConfig.firebaseProjectId,
    );

/// Handler de mensagens com o app em segundo plano/encerrado. Precisa ser função de nível
/// superior. Mensagens com bloco `notification` o sistema já exibe sozinho; aqui só garante
/// que o Firebase esteja inicializado para mensagens só de dados.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  if (Firebase.apps.isEmpty) await Firebase.initializeApp(options: _firebaseOptions);
}

/// Push (FCM/APNs). Sem as chaves do Firebase ([AppConfig.isPushConfigured]) o app roda
/// normalmente, só sem push.
abstract interface class PushService {
  /// [onRoute] recebe a rota JÁ VALIDADA ([safeInternalRoute]) de uma notificação tocada.
  Future<void> start({required void Function(String route) onRoute});

  /// Registra o aparelho no backend para este usuário.
  Future<void> registerDevice();
}

class NoopPushService implements PushService {
  @override
  Future<void> start({required void Function(String route) onRoute}) async {}

  @override
  Future<void> registerDevice() async {}
}

class FirebasePushService implements PushService {
  FirebasePushService({required this.api, required LocalNotifications localNotifications})
      : _local = localNotifications;

  final ApiClient api;
  final LocalNotifications _local;
  final _subscriptions = <StreamSubscription<Object?>>[];

  @override
  Future<void> start({required void Function(String route) onRoute}) async {
    if (Firebase.apps.isEmpty) await Firebase.initializeApp(options: _firebaseOptions);

    FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
    final messaging = FirebaseMessaging.instance;
    await messaging.requestPermission();

    // Em primeiro plano o sistema NÃO exibe a notificação: mostramos uma local.
    _subscriptions.add(FirebaseMessaging.onMessage.listen((message) {
      final notification = message.notification;
      if (notification == null) return;
      unawaited(_local.show(
        id: message.hashCode & 0x7fffffff,
        title: notification.title ?? AppConfig.appName,
        body: notification.body,
        payload: _routeOf(message),
      ));
    }));

    // App em segundo plano: o usuário tocou na notificação.
    _subscriptions.add(FirebaseMessaging.onMessageOpenedApp.listen((m) => _open(m, onRoute)));

    // App encerrado: foi aberto por uma notificação.
    final initial = await messaging.getInitialMessage();
    if (initial != null) _open(initial, onRoute);

    // ignore: deprecated_member_use
    _subscriptions.add(messaging.onTokenRefresh.listen((_) => unawaited(registerDevice())));
  }

  /// Valida a rota antes de navegar: o payload de uma notificação é entrada externa.
  String? _routeOf(RemoteMessage message) {
    final data = message.data['route'];
    return safeInternalRoute(data is String ? data : null);
  }

  void _open(RemoteMessage message, void Function(String route) onRoute) {
    final route = _routeOf(message);
    if (route != null) onRoute(route);
  }

  @override
  Future<void> registerDevice() async {
    // O token FCM clássico, que um backend com FCM HTTP v1 usa em `message.token`. O Firebase 17
    // depreciou `getToken` em favor de `register()`/`onRegistered`, que entregam o Installation
    // ID (FID) — OUTRO identificador, para "direct send". Trocar muda o contrato com o backend,
    // então isso fica para quando o endpoint de dispositivos for definido (ver ADR-067).
    // ignore: deprecated_member_use
    final token = await FirebaseMessaging.instance.getToken();
    if (token == null) return;

    try {
      await api.post('/devices/register', body: {
        'token': token,
        'platform': defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android',
      });
    } on ApiException catch (e) {
      // O endpoint ainda não existe no backend (404): o app segue sem push em vez de falhar.
      // Qualquer outro erro também não pode derrubar o login.
      debugPrint('Registro do aparelho não concluído (${e.status}).');
    }
  }
}
