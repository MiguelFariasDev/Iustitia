import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/info_row.dart';
import '../../../shared/widgets/states.dart';
import '../../processos/presentation/processo_card.dart';
import '../../processos/presentation/processos_controller.dart';
import 'clientes_controller.dart';

class ClienteDetalhePage extends ConsumerWidget {
  const ClienteDetalhePage({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final cliente = ref.watch(clienteProvider(id));

    return Scaffold(
      appBar: AppBar(title: const Text('Cliente')),
      body: switch (cliente) {
        AsyncData(:final value) => ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Semantics(
                header: true,
                child: Text(value.nome, style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
              ),
              const SizedBox(height: 8),
              InfoRow(label: 'Tipo', value: value.tipo.label),
              InfoRow(label: 'Documento', value: value.documento),
              InfoRow(label: 'E-mail', value: value.email),
              InfoRow(label: 'Telefone', value: value.telefone),
              if (value.dataNascimento != null)
                InfoRow(label: 'Nascimento', value: Formatters.date(value.dataNascimento!)),
              InfoRow(label: 'Endereço', value: value.endereco),
              if (value.observacoes != null) InfoRow(label: 'Observações', value: value.observacoes),
              const SizedBox(height: 24),
              Semantics(
                header: true,
                child: Text('Processos vinculados', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
              ),
              const SizedBox(height: 8),
              _ProcessosDoCliente(clienteId: value.id),
            ],
          ),
        AsyncError(:final error) =>
          ErrorView(error: error, onRetry: () => ref.invalidate(clienteProvider(id))),
        _ => const LoadingView(label: 'Carregando cliente…'),
      },
    );
  }
}

class _ProcessosDoCliente extends ConsumerWidget {
  const _ProcessosDoCliente({required this.clienteId});

  final String clienteId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return switch (ref.watch(processosDoClienteProvider(clienteId))) {
      AsyncData(:final value) when value.items.isEmpty => const EmptyView(
          icon: Icons.gavel_outlined,
          title: 'Nenhum processo vinculado',
        ),
      AsyncData(:final value) => Column(
          children: [
            for (final p in value.items)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: ProcessoCard(processo: p, onTap: () => context.go(AppRoutes.processo(p.id))),
              ),
            if (value.hasMore)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Mostrando ${value.items.length} de ${value.totalCount} processos. '
                  'A lista completa está no sistema web.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
          ],
        ),
      AsyncError(:final error) => ErrorView(
          error: error,
          onRetry: () => ref.invalidate(processosDoClienteProvider(clienteId)),
        ),
      _ => const LoadingView(label: 'Carregando processos…'),
    };
  }
}
