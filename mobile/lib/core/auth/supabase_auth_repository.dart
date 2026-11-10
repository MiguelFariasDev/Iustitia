import 'package:supabase_flutter/supabase_flutter.dart';

import 'auth_repository.dart';
import 'session_user.dart';
import 'jwt_claims.dart';

class SupabaseAuthRepository implements AuthRepository {
  SupabaseAuthRepository(this._client);

  final SupabaseClient _client;

  @override
  SessionUser? get currentUser => _toUser(_client.auth.currentSession);

  @override
  Stream<SessionUser?> get authChanges =>
      _client.auth.onAuthStateChange.map((state) => _toUser(state.session));

  @override
  Future<void> signIn({required String email, required String password}) async {
    try {
      await _client.auth.signInWithPassword(email: email.trim(), password: password);
    } on AuthException catch (e) {
      throw AuthFailure(_friendly(e));
    }
  }

  @override
  Future<void> signOut() => _client.auth.signOut();

  @override
  Future<String?> readAccessToken() async => _client.auth.currentSession?.accessToken;

  @override
  Future<String?> refreshAccessToken() async {
    try {
      final response = await _client.auth.refreshSession();
      return response.session?.accessToken;
    } on AuthException {
      return null;
    }
  }

  SessionUser? _toUser(Session? session) {
    if (session == null) return null;
    final user = session.user;
    return SessionUser.fromClaims(
      AccessTokenClaims.fromToken(session.accessToken),
      id: user.id,
      email: user.email ?? '',
    );
  }

  /// Não diferencia "e-mail inexistente" de "senha errada": dizer qual dos dois erros foi
  /// confirmaria a um estranho que o e-mail tem conta no escritório.
  String _friendly(AuthException e) {
    final code = e.code ?? '';
    if (code == 'invalid_credentials' || e.statusCode == '400') {
      return 'E-mail ou senha incorretos.';
    }
    if (e.statusCode == '429') return 'Muitas tentativas. Aguarde um instante e tente de novo.';
    return 'Não foi possível entrar. Tente novamente.';
  }
}
