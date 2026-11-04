import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

import '../app/config.dart';
import '../features/agenda/data/agenda_repository.dart';
import '../features/clientes/data/cliente_repository.dart';
import '../features/dashboard/data/dashboard_repository.dart';
import '../features/processos/data/processo_repository.dart';
import 'api/api_client.dart';
import 'auth/auth_controller.dart';
import 'auth/auth_repository.dart';
import 'auth/biometric_service.dart';
import 'auth/supabase_auth_repository.dart';
import 'mock/mock_auth_repository.dart';
import 'mock/mock_repositories.dart';
import 'notifications/alerts_realtime.dart';
import 'notifications/alerts_repository.dart';
import 'notifications/deadline_reminders.dart';
import 'notifications/local_notifications.dart';
import 'notifications/push_service.dart';
import 'settings/settings_store.dart';
import 'utils/external_actions.dart';

// --- Infraestrutura -------------------------------------------------------------------------

/// Carregado em `main` antes do `runApp` (e sobrescrito nos testes).
final sharedPreferencesProvider = Provider<SharedPreferences>(
  (ref) => throw UnimplementedError('sharedPreferencesProvider precisa ser sobrescrito'),
);

final settingsStoreProvider =
    Provider<SettingsStore>((ref) => SettingsStore(ref.watch(sharedPreferencesProvider)));

final biometricServiceProvider = Provider<BiometricService>((ref) => LocalAuthBiometricService());

final localNotificationsProvider =
    Provider<LocalNotifications>((ref) => FlutterLocalNotifications());

/// Relógio do app. Em um provider para os testes fixarem a data (agenda, saudação).
final clockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

final externalActionsProvider = Provider<ExternalActions>((ref) => PlatformExternalActions());

// --- Autenticação ---------------------------------------------------------------------------

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  if (AppConfig.isMockAuth) return MockAuthRepository();
  return SupabaseAuthRepository(Supabase.instance.client);
});

final authControllerProvider =
    NotifierProvider<AuthController, SessionState>(AuthController.new);

// --- API ------------------------------------------------------------------------------------

final apiClientProvider = Provider<ApiClient>((ref) {
  final auth = ref.watch(authRepositoryProvider);
  return ApiClient(
    baseUrl: AppConfig.apiUrl,
    readToken: auth.readAccessToken,
    refreshToken: auth.refreshAccessToken,
    onUnauthorized: () => ref.read(authControllerProvider.notifier).sessionExpired(),
  );
});

final processoRepositoryProvider = Provider<ProcessoRepository>((ref) =>
    AppConfig.useMocks
        ? MockProcessoRepository()
        : ApiProcessoRepository(ref.watch(apiClientProvider)));

final clienteRepositoryProvider = Provider<ClienteRepository>((ref) => AppConfig.useMocks
    ? MockClienteRepository()
    : ApiClienteRepository(ref.watch(apiClientProvider)));

final dashboardRepositoryProvider = Provider<DashboardRepository>((ref) => AppConfig.useMocks
    ? MockDashboardRepository()
    : ApiDashboardRepository(ref.watch(apiClientProvider)));

final alertsRepositoryProvider = Provider<AlertsRepository>((ref) => AppConfig.useMocks
    ? MockAlertsRepository()
    : ApiAlertsRepository(ref.watch(apiClientProvider)));

/// A agenda NÃO tem backend: é sempre a demonstração, independente de `API_MOCKS` (ligar a API
/// real hoje daria 404). A tela avisa. Ver [DemoAgendaRepository].
final agendaRepositoryProvider =
    Provider<AgendaRepository>((ref) => DemoAgendaRepository(now: ref.watch(clockProvider)));

/// A agenda de demonstração precisa avisar; uma agenda real não.
final agendaRepositoryProviderIsDemo = Provider<bool>((ref) => ref.watch(agendaRepositoryProvider).isDemo);

// --- Notificações ---------------------------------------------------------------------------

final alertsRealtimeProvider = Provider<AlertsRealtime>((ref) {
  if (AppConfig.isMockAuth) return MockAlertsRealtime();
  return SupabaseAlertsRealtime(Supabase.instance.client);
});

final pushServiceProvider = Provider<PushService>((ref) => AppConfig.isPushConfigured
    ? FirebasePushService(
        api: ref.watch(apiClientProvider),
        localNotifications: ref.watch(localNotificationsProvider),
      )
    : NoopPushService());

final deadlineReminderSchedulerProvider = Provider<DeadlineReminderScheduler>(
    (ref) => DeadlineReminderScheduler(ref.watch(localNotificationsProvider)));

/// Alertas que chegam em tempo real para o usuário logado. Vazio sem sessão ou sem escritório.
final incomingAlertsProvider = StreamProvider<AlertPayload>((ref) {
  final user = ref.watch(authControllerProvider.select((s) => s.isSignedIn ? s.user : null));
  final tenantId = user?.tenantId;
  if (user == null || tenantId == null) return const Stream.empty();

  return ref.watch(alertsRealtimeProvider).watch(tenantId: tenantId, userId: user.id);
});

/// Alertas não lidos (badge da aba Painel). Refaz a consulta a cada alerta que chega e, como
/// rede de segurança caso o Realtime caia, a cada minuto.
final unreadAlertsProvider = FutureProvider<int>((ref) async {
  final signedIn = ref.watch(authControllerProvider.select((s) => s.isSignedIn));
  if (!signedIn) return 0;

  ref.watch(incomingAlertsProvider);
  final timer = Timer(const Duration(minutes: 1), ref.invalidateSelf);
  ref.onDispose(timer.cancel);

  return ref.watch(alertsRepositoryProvider).unreadCount();
});

// --- Preferências ---------------------------------------------------------------------------

class ThemeModeController extends Notifier<ThemeMode> {
  @override
  ThemeMode build() => ref.watch(settingsStoreProvider).themeMode;

  Future<void> set(ThemeMode mode) async {
    state = mode;
    await ref.read(settingsStoreProvider).setThemeMode(mode);
  }
}

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);

final biometricLockProvider =
    Provider<bool>((ref) => ref.watch(settingsStoreProvider).biometricLock);

/// Se a biometria está disponível neste aparelho (decide se o botão aparece).
final biometricAvailableProvider =
    FutureProvider<bool>((ref) => ref.watch(biometricServiceProvider).isAvailable());

/// Se o app usa fontes do Google (baixadas em tempo de execução). Desligado nos testes.
final useGoogleFontsProvider = Provider<bool>((ref) => true);
