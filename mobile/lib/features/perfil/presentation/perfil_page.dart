import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/config.dart';
import '../../../core/providers.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/info_row.dart';

class PerfilPage extends ConsumerWidget {
  const PerfilPage({super.key});

  static const _roleLabels = {
    'Owner': 'Sócio-administrador',
    'Partner': 'Sócio',
    'Lawyer': 'Advogado(a)',
    'Assistant': 'Assistente',
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final user = ref.watch(authControllerProvider.select((s) => s.user));
    final themeMode = ref.watch(themeModeProvider);
    final lockEnabled = ref.watch(biometricLockProvider);
    final biometricAvailable = ref.watch(biometricAvailableProvider).value ?? false;

    return Scaffold(
      appBar: AppBar(title: const Text('Perfil')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (user != null) ...[
            Row(
              children: [
                ExcludeSemantics(
                  child: CircleAvatar(
                    radius: 30,
                    backgroundColor: theme.colorScheme.primary.withValues(alpha: 0.12),
                    child: Text(
                      Formatters.initials(user.displayName),
                      style: theme.textTheme.titleLarge?.copyWith(color: theme.colorScheme.primary),
                    ),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Semantics(
                    header: true,
                    child: Text(
                      user.displayName,
                      style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            InfoRow(label: 'E-mail', value: user.email),
            InfoRow(label: 'OAB', value: user.oab ?? 'Não informada'),
            InfoRow(label: 'Papel', value: _roleLabels[user.role] ?? user.role),
            const SizedBox(height: 16),
          ],
          const Divider(),
          Semantics(
            header: true,
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text('Aparência', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            ),
          ),
          SegmentedButton<ThemeMode>(
            segments: const [
              ButtonSegment(value: ThemeMode.system, label: Text('Sistema'), icon: Icon(Icons.brightness_auto)),
              ButtonSegment(value: ThemeMode.light, label: Text('Claro'), icon: Icon(Icons.light_mode_outlined)),
              ButtonSegment(value: ThemeMode.dark, label: Text('Escuro'), icon: Icon(Icons.dark_mode_outlined)),
            ],
            selected: {themeMode},
            onSelectionChanged: (s) => ref.read(themeModeProvider.notifier).set(s.first),
          ),
          const SizedBox(height: 24),
          Semantics(
            header: true,
            child: Text('Segurança e avisos', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
          ),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Bloquear com biometria'),
            subtitle: Text(
              biometricAvailable
                  ? 'Pede Face ID ou digital sempre que o app for aberto.'
                  : 'Cadastre uma biometria nas configurações do aparelho para usar.',
            ),
            value: lockEnabled,
            onChanged: biometricAvailable
                ? (value) async {
                    final ok = await ref.read(authControllerProvider.notifier).setBiometricLock(value);
                    if (!ok && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Não foi possível confirmar sua identidade.')),
                      );
                    }
                  }
                : null,
          ),
          // Lembretes de prazo dependem de prazos reais, que só existem a partir da Fase 3. Ligar
          // o interruptor hoje agendaria avisos sobre eventos de demonstração.
          const SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: Text('Lembretes de prazo'),
            subtitle: Text('Disponível quando a agenda estiver ligada ao servidor.'),
            value: false,
            onChanged: null,
          ),
          const SizedBox(height: 24),
          OutlinedButton.icon(
            onPressed: () => ref.read(authControllerProvider.notifier).signOut(),
            icon: const Icon(Icons.logout),
            label: const Text('Sair'),
          ),
          const SizedBox(height: 16),
          Center(
            child: Text(
              AppConfig.appName,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
        ],
      ),
    );
  }
}
