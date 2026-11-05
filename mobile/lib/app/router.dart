import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/providers.dart';
import '../core/utils/safe_route.dart';
import '../features/agenda/presentation/agenda_page.dart';
import '../features/auth/presentation/login_page.dart';
import '../features/clientes/presentation/cliente_detalhe_page.dart';
import '../features/clientes/presentation/clientes_page.dart';
import '../features/dashboard/presentation/dashboard_page.dart';
import '../features/perfil/presentation/perfil_page.dart';
import '../features/processos/presentation/processo_detalhe_page.dart';
import '../features/processos/presentation/processos_page.dart';
import 'shell.dart';

/// Caminhos do app. Qualquer rota vinda de fora (push, alerta) passa por [safeInternalRoute],
/// que só aceita o que está listado aqui.
abstract final class AppRoutes {
  static const login = '/login';
  static const dashboard = '/dashboard';
  static const processos = '/processos';
  static const clientes = '/clientes';
  static const agenda = '/agenda';
  static const perfil = '/perfil';

  static String processo(String id) => '$processos/$id';
  static String cliente(String id) => '$clientes/$id';
}

final routerProvider = Provider<GoRouter>((ref) {
  // O GoRouter reavalia o redirect quando este notifier muda: login, logout, bloqueio.
  final refresh = ValueNotifier<int>(0);
  ref.listen(authControllerProvider, (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);

  final router = GoRouter(
    initialLocation: AppRoutes.dashboard,
    refreshListenable: refresh,
    redirect: (context, state) {
      final signedIn = ref.read(authControllerProvider).isSignedIn;
      final onLogin = state.matchedLocation == AppRoutes.login;

      if (!signedIn) {
        if (onLogin) return null;
        // Guarda o destino (ex.: veio de uma notificação) para voltar a ele depois do login.
        final target = state.uri.toString();
        final isRoot = target == '/' || target == AppRoutes.dashboard;
        return isRoot
            ? AppRoutes.login
            : '${AppRoutes.login}?from=${Uri.encodeQueryComponent(target)}';
      }

      if (onLogin) {
        // `from` é parâmetro de URL, ou seja, entrada externa: só segue se for rota interna.
        return safeInternalRoute(state.uri.queryParameters['from']) ?? AppRoutes.dashboard;
      }
      if (state.matchedLocation == '/') return AppRoutes.dashboard;
      return null;
    },
    routes: [
      GoRoute(path: '/', redirect: (_, _) => AppRoutes.dashboard),
      GoRoute(path: AppRoutes.login, builder: (_, _) => const LoginPage()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: AppRoutes.dashboard, builder: (_, _) => const DashboardPage()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: AppRoutes.processos,
              builder: (_, _) => const ProcessosPage(),
              routes: [
                GoRoute(
                  path: ':id',
                  builder: (_, state) => ProcessoDetalhePage(id: state.pathParameters['id']!),
                ),
              ],
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: AppRoutes.clientes,
              builder: (_, _) => const ClientesPage(),
              routes: [
                GoRoute(
                  path: ':id',
                  builder: (_, state) => ClienteDetalhePage(id: state.pathParameters['id']!),
                ),
              ],
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: AppRoutes.agenda, builder: (_, _) => const AgendaPage()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: AppRoutes.perfil, builder: (_, _) => const PerfilPage()),
          ]),
        ],
      ),
    ],
  );

  ref.onDispose(router.dispose);
  return router;
});
