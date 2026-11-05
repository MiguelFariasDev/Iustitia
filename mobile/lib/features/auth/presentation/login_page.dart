import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/config.dart';
import '../../../core/auth/auth_repository.dart';
import '../../../core/providers.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _obscure = true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).signIn(
            email: _email.text,
            password: _password.text,
          );
      // Sucesso: o redirect do GoRouter leva ao painel; nada a fazer aqui.
    } on AuthFailure catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _unlock() async {
    final ok = await ref.read(authControllerProvider.notifier).unlockWithBiometrics();
    if (!ok && mounted) {
      setState(() => _error = 'Não foi possível confirmar sua identidade. Tente de novo ou entre com a senha.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final auth = ref.watch(authControllerProvider);
    final biometrics = ref.watch(biometricAvailableProvider).value ?? false;
    // Biometria só destrava uma sessão que já existe: sem sessão guardada não há o que liberar.
    final canUseBiometrics = biometrics && auth.hasSession;
    final problems = AppConfig.configurationProblems();

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    ExcludeSemantics(
                      child: Icon(Icons.balance_rounded, size: 56, color: theme.colorScheme.primary),
                    ),
                    const SizedBox(height: 16),
                    Semantics(
                      header: true,
                      child: Text(
                        AppConfig.appName,
                        textAlign: TextAlign.center,
                        style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      auth.hasSession
                          ? 'Bem-vindo de volta, ${auth.user!.displayName}.'
                          : 'Entre para acompanhar seus processos.',
                      textAlign: TextAlign.center,
                      style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                    const SizedBox(height: 32),
                    if (problems.isNotEmpty) ...[
                      _ConfigurationBanner(problems: problems),
                      const SizedBox(height: 16),
                    ],
                    if (canUseBiometrics) ...[
                      FilledButton.icon(
                        onPressed: _busy ? null : _unlock,
                        icon: const Icon(Icons.fingerprint_rounded),
                        label: const Text('Entrar com Face ID / biometria'),
                      ),
                      const SizedBox(height: 12),
                      const _OrDivider(),
                      const SizedBox(height: 12),
                    ],
                    TextFormField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.username, AutofillHints.email],
                      textInputAction: TextInputAction.next,
                      autocorrect: false,
                      decoration: const InputDecoration(labelText: 'E-mail'),
                      validator: (v) {
                        final value = v?.trim() ?? '';
                        if (value.isEmpty) return 'Informe seu e-mail.';
                        if (!value.contains('@')) return 'E-mail inválido.';
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _password,
                      obscureText: _obscure,
                      autofillHints: const [AutofillHints.password],
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _submit(),
                      decoration: InputDecoration(
                        labelText: 'Senha',
                        suffixIcon: IconButton(
                          tooltip: _obscure ? 'Mostrar senha' : 'Ocultar senha',
                          icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                          onPressed: () => setState(() => _obscure = !_obscure),
                        ),
                      ),
                      validator: (v) => (v == null || v.isEmpty) ? 'Informe sua senha.' : null,
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 16),
                      // liveRegion: leitores de tela anunciam o erro assim que ele aparece.
                      Semantics(
                        liveRegion: true,
                        child: Text(
                          _error!,
                          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                        ),
                      ),
                    ],
                    const SizedBox(height: 24),
                    canUseBiometrics
                        ? OutlinedButton(
                            onPressed: _busy ? null : _submit,
                            child: _busy ? const _ButtonSpinner() : const Text('Entrar com senha'),
                          )
                        : FilledButton(
                            onPressed: _busy ? null : _submit,
                            child: _busy ? const _ButtonSpinner() : const Text('Entrar'),
                          ),
                    if (auth.hasSession) ...[
                      const SizedBox(height: 12),
                      TextButton(
                        onPressed: _busy
                            ? null
                            : () => ref.read(authControllerProvider.notifier).signOut(),
                        child: const Text('Entrar com outra conta'),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ButtonSpinner extends StatelessWidget {
  const _ButtonSpinner();

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'Entrando',
        liveRegion: true,
        child: const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5)),
      );
}

class _OrDivider extends StatelessWidget {
  const _OrDivider();

  @override
  Widget build(BuildContext context) => const ExcludeSemantics(
        child: Row(children: [Expanded(child: Divider()), Padding(padding: EdgeInsets.all(8), child: Text('ou')), Expanded(child: Divider())]),
      );
}

class _ConfigurationBanner extends StatelessWidget {
  const _ConfigurationBanner({required this.problems});

  final List<String> problems;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: theme.colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Configuração incompleta', style: theme.textTheme.titleSmall),
          for (final p in problems) Text('• $p', style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}

