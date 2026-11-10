import 'session_user.dart';

/// Porta de autenticação. A implementação real usa o Supabase Auth; a de demonstração é um
/// login local (ver `core/mock`).
abstract interface class AuthRepository {
  /// Usuário da sessão restaurada do armazenamento seguro, ou `null`.
  SessionUser? get currentUser;

  /// Emite a cada login, logout ou expiração de sessão.
  Stream<SessionUser?> get authChanges;

  /// Lança [AuthFailure] com mensagem pronta para a tela.
  Future<void> signIn({required String email, required String password});

  Future<void> signOut();

  Future<String?> readAccessToken();

  /// Renova a sessão; `null` quando não é mais possível (refresh token inválido).
  Future<String?> refreshAccessToken();
}

class AuthFailure implements Exception {
  const AuthFailure(this.message);

  final String message;

  @override
  String toString() => 'AuthFailure($message)';
}
