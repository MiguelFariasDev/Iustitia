import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../shared/widgets/debounced_search_field.dart';
import '../../../shared/widgets/states.dart';
import '../../../shared/widgets/status_badge.dart';
import '../domain/cliente.dart';
import 'clientes_controller.dart';

class ClientesPage extends ConsumerWidget {
  const ClientesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final filter = ref.watch(clienteFilterProvider);
    final list = ref.watch(clientesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Clientes')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: DebouncedSearchField(
              hint: 'Buscar por nome ou CPF/CNPJ',
              onChanged: ref.read(clienteFilterProvider.notifier).setBusca,
            ),
          ),
          Expanded(
            child: switch (list) {
              AsyncData(:final value) when value.items.isEmpty => EmptyView(
                  icon: Icons.people_outline,
                  title: filter.busca.isNotEmpty ? 'Nenhum cliente encontrado' : 'Nenhum cliente',
                  message: filter.busca.isNotEmpty
                      ? 'Tente outro nome ou documento.'
                      : 'O cadastro de clientes é feito no sistema web.',
                ),
              AsyncData(:final value) => RefreshIndicator(
                  onRefresh: () => ref.refresh(clientesProvider.future),
                  child: NotificationListener<ScrollNotification>(
                    onNotification: (n) {
                      if (n.metrics.extentAfter < 300) ref.read(clientesProvider.notifier).loadMore();
                      return false;
                    },
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                      itemCount: value.items.length + (value.hasMore ? 1 : 0),
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        if (index >= value.items.length) {
                          return const Padding(
                            padding: EdgeInsets.all(16),
                            child: LoadingView(label: 'Carregando mais clientes…'),
                          );
                        }
                        return _ClienteCard(
                          cliente: value.items[index],
                          onTap: () => context.go(AppRoutes.cliente(value.items[index].id)),
                        );
                      },
                    ),
                  ),
                ),
              AsyncError(:final error) =>
                ErrorView(error: error, onRetry: () => ref.invalidate(clientesProvider)),
              _ => const LoadingView(label: 'Carregando clientes…'),
            },
          ),
        ],
      ),
    );
  }
}

class _ClienteCard extends StatelessWidget {
  const _ClienteCard({required this.cliente, required this.onTap});

  final ClienteListItem cliente;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Semantics(
      button: true,
      label: '${cliente.nome}, ${cliente.tipo.label}, documento ${cliente.documento}',
      excludeSemantics: true,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                CircleAvatar(
                  backgroundColor: theme.colorScheme.primary.withValues(alpha: 0.12),
                  child: Icon(
                    cliente.tipo == TipoCliente.pessoaJuridica ? Icons.business : Icons.person,
                    color: theme.colorScheme.primary,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(cliente.nome, style: theme.textTheme.titleSmall),
                      const SizedBox(height: 2),
                      Text(
                        cliente.documento,
                        style: theme.textTheme.bodySmall?.copyWith(
                          fontFamily: 'monospace',
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
                StatusBadge(label: cliente.tipo.label),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
