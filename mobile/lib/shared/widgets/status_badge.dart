import 'package:flutter/material.dart';

import '../../app/theme.dart';

enum BadgeTone { success, warning, danger, info, neutral }

/// Selo de status: fundo suave + texto de alto contraste. A cor nunca é o único indicador —
/// o texto sempre existe, e leitores de tela leem só ele.
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, this.tone = BadgeTone.neutral});

  final String label;
  final BadgeTone tone;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final colors = switch (tone) {
      BadgeTone.success => scheme.successTone,
      BadgeTone.warning => scheme.warningTone,
      BadgeTone.danger => scheme.dangerTone,
      BadgeTone.info => scheme.infoTone,
      BadgeTone.neutral => scheme.neutralTone,
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: colors.background,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: colors.foreground,
              fontWeight: FontWeight.w600,
            ),
      ),
    );
  }
}
