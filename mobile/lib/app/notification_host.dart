import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/notifications/alerts_repository.dart';
import '../core/providers.dart';
import '../core/utils/safe_route.dart';
import '../features/dashboard/presentation/dashboard_controller.dart';
import 'router.dart';

/// Chave do ScaffoldMessenger raiz: o banner de alerta aparece no TOPO de qualquer tela, sem
/// depender de qual Scaffold está ativo.
final rootMessengerKeyProvider =
    Provider<GlobalKey<ScaffoldMessengerState>>((ref) => GlobalKey<ScaffoldMessengerState>());

/// Liga o app às notificações enquanto há usuário logado:
/// - alerta em TEMPO REAL (Supabase Realtime) -> banner no topo + atualiza contador e painel;
/// - push (FCM/APNs) -> deep link para a tela certa ao tocar na notificação.
class NotificationHost extends ConsumerStatefulWidget {
  const NotificationHost({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<NotificationHost> createState() => _NotificationHostState();
}

class _NotificationHostState extends ConsumerState<NotificationHost> {
  bool _pushStarted = false;
  Timer? _bannerTimer;

  @override
  void initState() {
    super.initState();
    // Depois do primeiro frame: o router precisa estar montado para receber rotas.
    WidgetsBinding.instance.addPostFrameCallback((_) => _maybeStartPush());
  }

  /// Rota vinda de fora (push, notificação local): validada ANTES de navegar.
  void _openRoute(String? raw) {
    final route = safeInternalRoute(raw);
    if (route != null) ref.read(routerProvider).go(route);
  }

  Future<void> _maybeStartPush() async {
    if (_pushStarted || !ref.read(authControllerProvider).isSignedIn) return;
    _pushStarted = true;

    try {
      await ref.read(localNotificationsProvider).init(onTap: _openRoute);
      final push = ref.read(pushServiceProvider);
      await push.start(onRoute: _openRoute);
      await push.registerDevice();
    } on Object catch (e) {
      // Push é conveniência: falhar aqui nunca pode impedir o uso do app.
      debugPrint('Push não iniciado: $e');
      _pushStarted = false;
    }
  }

  Future<void> _showBanner(AlertPayload alert) async {
    final messenger = ref.read(rootMessengerKeyProvider).currentState;
    if (messenger == null) return;

    // O destino é resolvido ANTES de mostrar o banner, para o toque ser instantâneo.
    final route = await ref.read(alertsRepositoryProvider).routeFor(alert);

    messenger
      ..hideCurrentMaterialBanner()
      ..showMaterialBanner(
        MaterialBanner(
          // Anunciado por leitores de tela assim que aparece.
          content: Semantics(
            liveRegion: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(alert.title, style: const TextStyle(fontWeight: FontWeight.w700)),
                if (alert.description != null) Text(alert.description!),
              ],
            ),
          ),
          leading: const Icon(Icons.notifications_active_outlined),
          actions: [
            TextButton(
              onPressed: messenger.hideCurrentMaterialBanner,
              child: const Text('Dispensar'),
            ),
            if (route != null)
              TextButton(
                onPressed: () {
                  messenger.hideCurrentMaterialBanner();
                  _openRoute(route);
                },
                child: const Text('Ver'),
              ),
          ],
        ),
      );

    // Some sozinho: um banner esquecido cobriria o topo da tela para sempre. Um alerta novo
    // reinicia a contagem em vez de somar timers.
    _bannerTimer?.cancel();
    _bannerTimer = Timer(const Duration(seconds: 8), messenger.hideCurrentMaterialBanner);
  }

  @override
  void dispose() {
    _bannerTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(incomingAlertsProvider, (_, next) {
      final alert = next.value;
      if (alert != null) {
        unawaited(_showBanner(alert));
        // O alerta já está no banco quando o broadcast chega: atualiza o painel e o contador.
        ref.invalidate(dashboardProvider);
      }
    });

    ref.listen(authControllerProvider.select((s) => s.isSignedIn), (_, signedIn) {
      if (signedIn) {
        unawaited(_maybeStartPush());
      } else {
        _pushStarted = false;
        ref.read(rootMessengerKeyProvider).currentState?.hideCurrentMaterialBanner();
      }
    });

    return widget.child;
  }
}
