import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:timezone/data/latest_all.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

/// Notificações locais: push recebido com o app aberto e lembretes de prazo agendados.
abstract interface class LocalNotifications {
  /// [onTap] recebe o `payload` (a rota de destino) quando o usuário toca na notificação.
  Future<void> init({required void Function(String? payload) onTap});

  /// Pede permissão (Android 13+/iOS). `false` se o usuário negou.
  Future<bool> requestPermission();

  Future<void> show({required int id, required String title, String? body, String? payload});

  Future<void> scheduleAt({
    required int id,
    required DateTime when,
    required String title,
    String? body,
    String? payload,
  });

  Future<void> cancelAll();
}

class FlutterLocalNotifications implements LocalNotifications {
  FlutterLocalNotifications([FlutterLocalNotificationsPlugin? plugin])
      : _plugin = plugin ?? FlutterLocalNotificationsPlugin();

  final FlutterLocalNotificationsPlugin _plugin;

  static const _channel = AndroidNotificationChannel(
    'alertas',
    'Alertas',
    description: 'Publicações, prazos e avisos do escritório',
    importance: Importance.high,
  );

  static const _details = NotificationDetails(
    android: AndroidNotificationDetails(
      'alertas',
      'Alertas',
      channelDescription: 'Publicações, prazos e avisos do escritório',
      importance: Importance.high,
      priority: Priority.high,
    ),
    iOS: DarwinNotificationDetails(),
  );

  @override
  Future<void> init({required void Function(String? payload) onTap}) async {
    tzdata.initializeTimeZones();

    await _plugin.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('@mipmap/ic_launcher'),
        // A permissão é pedida no momento certo (requestPermission), não na abertura.
        iOS: DarwinInitializationSettings(
          requestAlertPermission: false,
          requestBadgePermission: false,
          requestSoundPermission: false,
        ),
      ),
      onDidReceiveNotificationResponse: (response) => onTap(response.payload),
    );

    await _plugin
        .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
        ?.createNotificationChannel(_channel);
  }

  @override
  Future<bool> requestPermission() async {
    final android = await _plugin
        .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
        ?.requestNotificationsPermission();
    final ios = await _plugin
        .resolvePlatformSpecificImplementation<IOSFlutterLocalNotificationsPlugin>()
        ?.requestPermissions(alert: true, badge: true, sound: true);
    return android ?? ios ?? false;
  }

  @override
  Future<void> show({required int id, required String title, String? body, String? payload}) =>
      _plugin.show(id: id, title: title, body: body, notificationDetails: _details, payload: payload);

  @override
  Future<void> scheduleAt({
    required int id,
    required DateTime when,
    required String title,
    String? body,
    String? payload,
  }) =>
      _plugin.zonedSchedule(
        id: id,
        // Um instante absoluto: UTC dispensa descobrir o fuso do aparelho.
        scheduledDate: tz.TZDateTime.from(when, tz.UTC),
        title: title,
        body: body,
        notificationDetails: _details,
        // Inexato: não exige a permissão de "alarme exato" e a diferença de minutos não importa
        // para um lembrete de prazo.
        androidScheduleMode: AndroidScheduleMode.inexactAllowWhileIdle,
        payload: payload,
      );

  @override
  Future<void> cancelAll() => _plugin.cancelAll();
}
