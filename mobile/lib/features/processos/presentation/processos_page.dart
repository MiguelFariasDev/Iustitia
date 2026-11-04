import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../shared/widgets/debounced_search_field.dart';
import '../../../shared/widgets/states.dart';
import '../domain/processo.dart';
import 'processo_card.dart';
import 'processos_controller.dart';

class ProcessosPage extends ConsumerWidget {
  const ProcessosPage({super.key});

  // O pedido é "Ativos" e "Arquivados". Suspensos e encerrados aparecem em "todos".
  static const _filters = [StatusProcesso.ativo, StatusProcesso.arquivado];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final filter = ref.watch(processoFilterProvider);
    final list = ref.watch(processosProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Processos')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: DebouncedSearchField(
              hint: 'Buscar por CNJ, cliente ou parte',
              onChanged: ref.read(processoFilterProvider.notifier).setBusca,
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Wrap(
                spacing: 8,
                children: [
                  for (final status in _filters)
                    FilterChip(
                      label: Text(status == StatusProcesso.ativo ? 'Ativos' : 'Arquivados'),
                      selected: filter.status == status,
                      onSelected: (_) => ref.read(processoFilterProvider.notifier).toggleStatus(status),
                    ),
                ],
              ),
            ),
          ),
          Expanded(
            child: switch (list) {
              AsyncData(:final value) when value.items.isEmpty => EmptyView(
                  icon: Icons.gavel_outlined,
                  title: filter.busca.isNotEmpty ? 'Nenhum processo encontrado' : 'Nenhum processo',
                  message: filter.busca.isNotEmpty
                      ? 'Tente outro número, cliente ou parte.'
                      : 'O cadastro de processos é feito no sistema web.',
                ),
              AsyncData(:final value) => RefreshIndicator(
                  onRefresh: () => ref.refresh(processosProvider.future),
                  child: NotificationListener<ScrollNotification>(
                    // Chega perto do fim: pede a próxima página.
                    onNotification: (n) {
                      if (n.metrics.extentAfter < 300) {
                        ref.read(processosProvider.notifier).loadMore();
                      }
                      return false;
                    },
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                      itemCount: value.items.length + (value.hasMore || value.loadMoreFailed ? 1 : 0),
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        if (index >= value.items.length) {
                          return value.loadMoreFailed
                              ? Center(
                                  child: TextButton(
                                    onPressed: () => ref.read(processosProvider.notifier).loadMore(),
                                    child: const Text('Não foi possível carregar mais. Tentar de novo'),
                                  ),
                                )
                              : const Padding(
                                  padding: EdgeInsets.all(16),
                                  child: LoadingView(label: 'Carregando mais processos…'),
                                );
                        }
                        final processo = value.items[index];
                        return ProcessoCard(
                          processo: processo,
                          onTap: () => context.go(AppRoutes.processo(processo.id)),
                        );
                      },
                    ),
                  ),
                ),
              AsyncError(:final error) =>
                ErrorView(error: error, onRetry: () => ref.invalidate(processosProvider)),
              _ => const LoadingView(label: 'Carregando processos…'),
            },
          ),
        ],
      ),
    );
  }
}
