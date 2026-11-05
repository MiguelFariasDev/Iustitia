import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/widgets/status_badge.dart';
import '../domain/processo.dart';
import 'processos_controller.dart';

BadgeTone toneOf(StatusProcesso status) => switch (status) {
      StatusProcesso.ativo => BadgeTone.success,
      StatusProcesso.suspenso => BadgeTone.warning,
      StatusProcesso.encerrado || StatusProcesso.arquivado => BadgeTone.neutral,
    };

class ProcessoCard extends ConsumerWidget {
  const ProcessoCard({super.key, required this.processo, required this.onTap});

  final ProcessoListItem processo;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    // Enquanto o nome carrega (ou se falhar), o cartão continua útil sem ele.
    final cliente = ref.watch(clienteNomeProvider(processo.clienteId)).value;

    final summary = [
      'Processo ${processo.cnjNumber}',
      if (cliente != null) 'cliente $cliente',
      processo.area.label,
      processo.status.label,
    ].join(', ');

    return Semantics(
      button: true,
      label: summary,
      excludeSemantics: true,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        processo.cnjNumber,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontFamily: 'monospace',
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    StatusBadge(label: processo.status.label, tone: toneOf(processo.status)),
                  ],
                ),
                const SizedBox(height: 8),
                if (cliente != null) Text(cliente, style: theme.textTheme.bodyLarge),
                const SizedBox(height: 4),
                Text(
                  [
                    processo.area.label,
                    if (processo.tribunal != null) processo.tribunal!,
                    if (processo.parteContraria != null) 'x ${processo.parteContraria}',
                  ].join(' · '),
                  style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
