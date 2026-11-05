import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/info_row.dart';
import '../../../shared/widgets/states.dart';
import '../../../shared/widgets/status_badge.dart';
import '../domain/processo.dart';
import 'processo_card.dart';
import 'processos_controller.dart';

/// Consulta unificada de processos do CNJ (PDPJ). Não aceita o número por URL, então o app
/// copia o número e abre o portal: é o que dá para fazer SEM inventar o endereço de cada
/// tribunal (sistemas e URLs diferem por tribunal e mudam).
final _portalTribunais = Uri.parse('https://portaldeservicos.pdpj.jus.br/consulta');

class ProcessoDetalhePage extends ConsumerWidget {
  const ProcessoDetalhePage({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final processo = ref.watch(processoProvider(id));

    return DefaultTabController(
      length: 3,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Processo'),
          actions: [
            if (processo.value case final p?) ...[
              IconButton(
                tooltip: 'Compartilhar',
                icon: const Icon(Icons.share_outlined),
                onPressed: () => ref
                    .read(externalActionsProvider)
                    .share(text: 'Processo ${p.cnjNumber}${p.tribunal == null ? '' : ' — ${p.tribunal}'}'),
              ),
              IconButton(
                tooltip: 'Ver no site do tribunal',
                icon: const Icon(Icons.open_in_new_rounded),
                onPressed: () => _openTribunal(context, ref, p),
              ),
            ],
          ],
          bottom: const TabBar(
            tabs: [Tab(text: 'Dados'), Tab(text: 'Publicações'), Tab(text: 'Movimentos')],
          ),
        ),
        body: switch (processo) {
          AsyncData(:final value) => TabBarView(
              children: [
                _DadosTab(processo: value),
                _PublicacoesTab(processId: value.id),
                const _MovimentosTab(),
              ],
            ),
          AsyncError(:final error) =>
            ErrorView(error: error, onRetry: () => ref.invalidate(processoProvider(id))),
          _ => const LoadingView(label: 'Carregando processo…'),
        },
      ),
    );
  }

  Future<void> _openTribunal(BuildContext context, WidgetRef ref, Processo p) async {
    final actions = ref.read(externalActionsProvider);
    await actions.copy(p.cnjNumber);
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Número copiado. Cole na consulta do portal.')),
      );
    }
    await actions.openUrl(_portalTribunais);
  }
}

class _DadosTab extends ConsumerWidget {
  const _DadosTab({required this.processo});

  final Processo processo;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final cliente = ref.watch(clienteNomeProvider(processo.clienteId)).value;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Semantics(
          header: true,
          child: Text(
            processo.cnjNumber,
            style: theme.textTheme.titleLarge?.copyWith(fontFamily: 'monospace', fontWeight: FontWeight.w700),
          ),
        ),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerLeft,
          child: StatusBadge(label: processo.status.label, tone: toneOf(processo.status)),
        ),
        const SizedBox(height: 12),
        InfoRow(label: 'Cliente', value: cliente),
        InfoRow(label: 'Área', value: processo.area.label),
        InfoRow(label: 'Assunto', value: processo.assunto),
        InfoRow(label: 'Parte contrária', value: processo.parteContraria),
        InfoRow(label: 'Tribunal', value: processo.tribunal),
        InfoRow(label: 'Comarca', value: processo.comarca),
        InfoRow(label: 'Vara', value: processo.vara),
        InfoRow(
          label: 'Valor da causa',
          value: processo.valorCausa == null ? null : Formatters.currency(processo.valorCausa!),
        ),
        InfoRow(
          label: 'Distribuição',
          value: processo.dataDistribuicao == null ? null : Formatters.date(processo.dataDistribuicao!),
        ),
        InfoRow(
          label: 'Acompanhamento',
          value: processo.geraCaptura ? 'Monitorado nos diários' : 'Não monitorado',
        ),
        if (processo.observacoes != null) InfoRow(label: 'Observações', value: processo.observacoes),
      ],
    );
  }
}

class _PublicacoesTab extends ConsumerWidget {
  const _PublicacoesTab({required this.processId});

  final String processId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);

    return switch (ref.watch(publicacoesDoProcessoProvider(processId))) {
      AsyncData(:final value) when value.items.isEmpty => const EmptyView(
          icon: Icons.mark_email_read_outlined,
          title: 'Nenhuma publicação',
          message: 'As publicações dos diários oficiais deste processo aparecem aqui.',
        ),
      AsyncData(:final value) => ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: value.items.length,
          separatorBuilder: (_, _) => const SizedBox(height: 8),
          itemBuilder: (context, i) {
            final p = value.items[i];
            return Card(
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Wrap: com tipo longo ("Intimação…") e "Não lida" os selos quebram de
                        // linha em vez de estourar a tela estreita.
                        Expanded(
                          child: Wrap(
                            spacing: 8,
                            runSpacing: 4,
                            children: [
                              if (p.type != null) StatusBadge(label: p.type!, tone: BadgeTone.info),
                              if (!p.isRead) const StatusBadge(label: 'Não lida', tone: BadgeTone.warning),
                            ],
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          Formatters.date(p.publishedAt),
                          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(p.preview, style: theme.textTheme.bodyMedium),
                  ],
                ),
              ),
            );
          },
        ),
      AsyncError(:final error) => ErrorView(
          error: error,
          onRetry: () => ref.invalidate(publicacoesDoProcessoProvider(processId)),
        ),
      _ => const LoadingView(label: 'Carregando publicações…'),
    };
  }
}

/// As movimentações do tribunal ainda não chegam ao backend (a consulta ao DataJud devolve só
/// metadados do cadastro). Dizer isso é melhor que uma lista vazia que pareça "sem movimentos".
class _MovimentosTab extends StatelessWidget {
  const _MovimentosTab();

  @override
  Widget build(BuildContext context) => const EmptyView(
        icon: Icons.timeline_outlined,
        title: 'Movimentações em breve',
        message: 'O andamento processual dos tribunais ainda não está disponível no app. '
            'Use "Ver no site do tribunal" para consultar.',
      );
}
