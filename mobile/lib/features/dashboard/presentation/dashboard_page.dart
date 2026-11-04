import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../core/providers.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/states.dart';
import '../../../shared/widgets/status_badge.dart';
import '../domain/dashboard_data.dart';
import 'dashboard_controller.dart';

class DashboardPage extends ConsumerWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboard = ref.watch(dashboardProvider);
    final user = ref.watch(authControllerProvider.select((s) => s.user));
    final theme = Theme.of(context);
    final now = ref.watch(clockProvider)();

    // "Dr(a).": não presume o gênero de quem usa o app.
    final name = Formatters.firstName(user?.displayName ?? '');
    final greeting = '${Formatters.greeting(now)}${name.isEmpty ? '' : ', Dr(a). $name'}';

    return Scaffold(
      appBar: AppBar(title: const Text('Painel')),
      body: RefreshIndicator(
        // Aguarda a consulta nova para o indicador só sumir quando os dados chegarem.
        onRefresh: () async {
          ref.invalidate(unreadAlertsProvider);
          await ref.refresh(dashboardProvider.future).catchError((_) => const DashboardData(
                activeProcesses: 0,
                unreadPublications: 0,
                pendingReview: 0,
                recentPublications: [],
              ));
        },
        child: ListView(
          // Mesmo com pouco conteúdo, a lista precisa rolar para o pull-to-refresh funcionar.
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16),
          children: [
            Semantics(
              header: true,
              child: Text(greeting, style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
            ),
            const SizedBox(height: 4),
            Text(
              Formatters.weekdayLong(now),
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 20),
            ...switch (dashboard) {
              AsyncData(:final value) => [
                  _SummaryCards(data: value),
                  const SizedBox(height: 24),
                  _RecentMovements(data: value),
                ],
              AsyncError(:final error) => [
                  ErrorView(error: error, onRetry: () => ref.invalidate(dashboardProvider)),
                ],
              _ => const [LoadingView(label: 'Carregando painel…')],
            },
          ],
        ),
      ),
    );
  }
}

class _SummaryCards extends StatelessWidget {
  const _SummaryCards({required this.data});

  final DashboardData data;

  @override
  Widget build(BuildContext context) {
    // Os dois primeiros são dados reais. "Próximos prazos" NÃO tem número: prazos só existem a
    // partir da Fase 3, e um contador inventado levaria a decisão errada (ver DashboardData).
    return Column(
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: SummaryCard(
                icon: Icons.gavel,
                label: 'Processos ativos',
                value: '${data.activeProcesses}',
                onTap: () => context.go(AppRoutes.processos),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: SummaryCard(
                icon: Icons.mark_email_unread_outlined,
                label: 'Publicações não lidas',
                value: '${data.unreadPublications}',
                onTap: () => context.go(AppRoutes.processos),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        SummaryCard(
          icon: Icons.event_available_outlined,
          label: 'Próximos prazos',
          value: 'Em breve',
          hint: 'Prazos chegam com a agenda ligada ao servidor.',
          onTap: () => context.go(AppRoutes.agenda),
        ),
      ],
    );
  }
}

/// Cartão de resumo clicável: o cartão INTEIRO é o alvo de toque e é lido como um botão.
class SummaryCard extends StatelessWidget {
  const SummaryCard({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
    required this.onTap,
    this.hint,
  });

  final IconData icon;
  final String label;
  final String value;
  final String? hint;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Semantics(
      button: true,
      label: '$label: $value${hint == null ? '' : '. $hint'}',
      excludeSemantics: true,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(icon, size: 20, color: theme.colorScheme.secondary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        label,
                        style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Text(value, style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700)),
                if (hint != null) ...[
                  const SizedBox(height: 4),
                  Text(hint!, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RecentMovements extends StatelessWidget {
  const _RecentMovements({required this.data});

  final DashboardData data;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          header: true,
          child: Text('Últimas movimentações', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
        ),
        const SizedBox(height: 8),
        if (data.recentPublications.isEmpty)
          const EmptyView(
            icon: Icons.inbox_outlined,
            title: 'Nenhuma publicação pendente',
            message: 'Quando algo chegar dos diários oficiais, aparece aqui.',
          )
        else
          for (final p in data.recentPublications)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Card(
                clipBehavior: Clip.antiAlias,
                child: InkWell(
                  // Sem processo vinculado não há para onde ir: o cartão fica inerte.
                  onTap: p.processId == null ? null : () => context.go(AppRoutes.processo(p.processId!)),
                  child: Padding(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            if (p.tribunal != null) ...[
                              StatusBadge(label: p.tribunal!, tone: BadgeTone.info),
                              const SizedBox(width: 8),
                            ],
                            Expanded(
                              child: Text(
                                p.cnjNumber,
                                style: theme.textTheme.bodySmall?.copyWith(fontFamily: 'monospace'),
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            Text(
                              Formatters.relative(p.publishedAt),
                              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                            ),
                          ],
                        ),
                        if (p.clienteNome != null) ...[
                          const SizedBox(height: 8),
                          Text(p.clienteNome!, style: theme.textTheme.titleSmall),
                        ],
                        const SizedBox(height: 4),
                        Text(
                          p.preview,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
      ],
    );
  }
}
