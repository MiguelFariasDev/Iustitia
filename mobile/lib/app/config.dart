/// Configuração de build, lida de `--dart-define` (ou `--dart-define-from-file=env/dev.json`).
///
/// Nenhuma URL, chave ou identificador de ambiente fica no código: o que muda por ambiente
/// entra na linha de comando do build. `env/example.json` mostra todas as chaves; os arquivos
/// reais (`env/dev.json`, `env/prod.json`) são ignorados pelo git.
///
/// As chaves do Supabase (anon) e do Firebase (API key) são identificadores públicos de
/// cliente — a proteção dos dados está no RLS e nas regras do projeto, não no sigilo delas —,
/// mas mesmo assim não são versionadas, para o repositório servir a qualquer ambiente.
abstract final class AppConfig {
  /// Nome de produto exibido ao usuário. Marca desacoplada: nada de nome de produto em
  /// pacote, bundle ID ou classe — só aqui, e configurável.
  static const appName = String.fromEnvironment('APP_NAME', defaultValue: 'Advocacia');

  /// Base da API REST, ex.: `https://api.exemplo.com.br/api/v1` (sem barra no fim).
  static const apiUrl = String.fromEnvironment('API_URL');

  static const supabaseUrl = String.fromEnvironment('SUPABASE_URL');
  static const supabaseAnonKey = String.fromEnvironment('SUPABASE_ANON_KEY');

  /// `mock` troca o Supabase Auth por um login local de demonstração (só dev/testes).
  static const authMode = String.fromEnvironment('AUTH_MODE', defaultValue: 'supabase');

  /// Usa repositórios em memória no lugar da API. Padrão **falso**: um build de produção
  /// esquecido com mock mostraria dados inventados como se fossem do escritório.
  static const useMocks = bool.fromEnvironment('API_MOCKS');

  static const firebaseApiKey = String.fromEnvironment('FIREBASE_API_KEY');
  static const firebaseAppId = String.fromEnvironment('FIREBASE_APP_ID');
  static const firebaseSenderId = String.fromEnvironment('FIREBASE_MESSAGING_SENDER_ID');
  static const firebaseProjectId = String.fromEnvironment('FIREBASE_PROJECT_ID');

  static bool get isMockAuth => authMode == 'mock';

  static bool get isSupabaseConfigured => supabaseUrl.isNotEmpty && supabaseAnonKey.isNotEmpty;

  /// Push só liga com as quatro chaves do Firebase; sem elas o app roda normalmente,
  /// apenas sem notificações push (o Realtime in-app continua funcionando).
  static bool get isPushConfigured =>
      firebaseApiKey.isNotEmpty &&
      firebaseAppId.isNotEmpty &&
      firebaseSenderId.isNotEmpty &&
      firebaseProjectId.isNotEmpty;

  /// Problemas que impedem o app de funcionar de verdade, em linguagem de quem configura.
  /// Lista vazia = configuração suficiente.
  static List<String> configurationProblems() {
    final problems = <String>[];
    if (!isMockAuth && !isSupabaseConfigured) {
      problems.add('Defina SUPABASE_URL e SUPABASE_ANON_KEY (ou AUTH_MODE=mock para demonstração).');
    }
    if (!useMocks && apiUrl.isEmpty) {
      problems.add('Defina API_URL (ou API_MOCKS=true para demonstração).');
    }
    return problems;
  }
}
