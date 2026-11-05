import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

import 'app/app.dart';
import 'app/config.dart';
import 'core/auth/secure_local_storage.dart';
import 'core/providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('pt_BR');

  final problems = AppConfig.configurationProblems();
  if (problems.isNotEmpty && !AppConfig.isMockAuth && !AppConfig.isSupabaseConfigured) {
    // Sem Supabase não há o que inicializar; mostra o que falta em vez de quebrar com um
    // erro de "Supabase.instance" sem contexto.
    runApp(_ConfigurationErrorApp(problems: problems));
    return;
  }

  if (!AppConfig.isMockAuth) {
    await Supabase.initialize(
      url: AppConfig.supabaseUrl,
      // `anonKey` está depreciado em favor de `publishableKey` (chaves novas `sb_publishable_…`).
      // O projeto web usa a chave anon legada, então o app segue o mesmo contrato por enquanto;
      // migrar os dois juntos quando o projeto Supabase trocar de formato de chave.
      // ignore: deprecated_member_use
      anonKey: AppConfig.supabaseAnonKey,
      // Sessão no Keychain/Keystore, não em shared_preferences (texto puro).
      authOptions: const FlutterAuthClientOptions(localStorage: SecureLocalStorage()),
    );
  }

  final prefs = await SharedPreferences.getInstance();

  runApp(
    ProviderScope(
      // O Riverpod 3 refaz sozinho providers que falharam; nas telas isso esconderia o erro e
      // repetiria chamadas. O usuário decide quando tentar de novo ("Tentar novamente").
      retry: (_, _) => null,
      overrides: [sharedPreferencesProvider.overrideWithValue(prefs)],
      child: const AdvocaciaApp(),
    ),
  );
}

class _ConfigurationErrorApp extends StatelessWidget {
  const _ConfigurationErrorApp({required this.problems});

  final List<String> problems;

  @override
  Widget build(BuildContext context) => MaterialApp(
        debugShowCheckedModeBanner: false,
        home: Scaffold(
          body: SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Configuração incompleta', style: Theme.of(context).textTheme.headlineSmall),
                  const SizedBox(height: 16),
                  for (final p in problems) Text('• $p'),
                  const SizedBox(height: 16),
                  const Text('Veja mobile/README.md e env/example.json.'),
                ],
              ),
            ),
          ),
        ),
      );
}
