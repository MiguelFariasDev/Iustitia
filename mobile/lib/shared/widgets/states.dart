import 'package:flutter/material.dart';

import '../../core/errors/app_exception.dart';

/// Carregando, com rótulo para leitores de tela.
class LoadingView extends StatelessWidget {
  const LoadingView({super.key, this.label = 'Carregando…'});

  final String label;

  @override
  Widget build(BuildContext context) => Center(
        child: Semantics(
          label: label,
          liveRegion: true,
          child: const Padding(
            padding: EdgeInsets.all(32),
            child: CircularProgressIndicator(),
          ),
        ),
      );
}

/// Estado vazio amigável.
class EmptyView extends StatelessWidget {
  const EmptyView({super.key, required this.icon, required this.title, this.message, this.action});

  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ExcludeSemantics(
              child: Icon(icon, size: 48, color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: 8),
              Text(
                message!,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
            ],
            if (action != null) ...[const SizedBox(height: 16), action!],
          ],
        ),
      ),
    );
  }
}

/// Erro de carregamento com "Tentar novamente".
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.error, this.onRetry});

  final Object error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final isNetwork = error is ApiException && (error as ApiException).isNetworkError;
    return EmptyView(
      icon: isNetwork ? Icons.wifi_off_rounded : Icons.error_outline_rounded,
      title: 'Não foi possível carregar',
      message: errorMessage(error),
      action: onRetry == null
          ? null
          : OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('Tentar novamente'),
            ),
    );
  }
}

/// Aviso fixo de dados de demonstração (mesma ideia do `DemoNotice` do web): nada ali é do
/// escritório, e quem olha precisa saber sem adivinhar.
class DemoNotice extends StatelessWidget {
  const DemoNotice({super.key, required this.module});

  final String module;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Semantics(
      container: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          border: Border.all(color: theme.colorScheme.outlineVariant),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text.rich(
          TextSpan(children: [
            const TextSpan(
              text: 'Dados de demonstração. ',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
            TextSpan(
              text: '$module ainda não está ligada ao servidor — nada aqui é do seu escritório.',
            ),
          ]),
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
      ),
    );
  }
}
