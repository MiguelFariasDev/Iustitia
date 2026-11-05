import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/providers.dart';

/// Casca com a barra de navegação inferior. Cada aba mantém a própria pilha (voltar de um
/// detalhe para a lista preserva busca e rolagem).
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Não há aba de alertas: o contador de não lidos fica no ícone do Painel, que é onde o
    // usuário vai olhar o que chegou.
    final unread = ref.watch(unreadAlertsProvider).value ?? 0;

    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        // Tocar na aba já ativa volta à raiz dela (convenção de apps móveis).
        onDestinationSelected: (index) =>
            shell.goBranch(index, initialLocation: index == shell.currentIndex),
        destinations: [
          NavigationDestination(
            icon: _DashboardIcon(unread: unread, selected: false),
            selectedIcon: _DashboardIcon(unread: unread, selected: true),
            label: 'Painel',
            tooltip: unread > 0 ? 'Painel, $unread alertas não lidos' : 'Painel',
          ),
          const NavigationDestination(
            icon: Icon(Icons.gavel_outlined),
            selectedIcon: Icon(Icons.gavel),
            label: 'Processos',
          ),
          const NavigationDestination(
            icon: Icon(Icons.people_outline),
            selectedIcon: Icon(Icons.people),
            label: 'Clientes',
          ),
          const NavigationDestination(
            icon: Icon(Icons.calendar_month_outlined),
            selectedIcon: Icon(Icons.calendar_month),
            label: 'Agenda',
          ),
          const NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Perfil',
          ),
        ],
      ),
    );
  }
}

class _DashboardIcon extends StatelessWidget {
  const _DashboardIcon({required this.unread, required this.selected});

  final int unread;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final icon = Icon(selected ? Icons.dashboard : Icons.dashboard_outlined);
    if (unread == 0) return icon;

    return Badge(label: Text(unread > 99 ? '99+' : '$unread'), child: icon);
  }
}
