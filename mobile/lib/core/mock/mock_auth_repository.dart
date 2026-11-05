import 'dart:async';

import '../auth/auth_repository.dart';
import '../auth/session_user.dart';

/// Login local de demonstração (`AUTH_MODE=mock`): sem Supabase. Mesmas credenciais de demo do
/// frontend web. NUNCA ligado em produção — `AppConfig.isMockAuth` só é verdadeiro por
/// `--dart-define` explícito.
class MockAuthRepository implements AuthRepository {
  MockAuthRepository({SessionUser? signedIn}) : _user = signedIn;

  static const demoEmail = 'ana.souza@escritorio.com.br';
  static const demoPassword = 'demo1234';

  static const demoUser = SessionUser(
    id: '00000000-0000-4000-8000-0000000000a1',
    email: demoEmail,
    tenantId: '00000000-0000-4000-8000-0000000000aa',
    role: 'Partner',
    name: 'Ana Souza',
    oab: 'OAB/SP 123.456',
  );

  final _controller = StreamController<SessionUser?>.broadcast();
  SessionUser? _user;

  @override
  SessionUser? get currentUser => _user;

  @override
  Stream<SessionUser?> get authChanges => _controller.stream;

  @override
  Future<void> signIn({required String email, required String password}) async {
    if (email.trim().toLowerCase() != demoEmail || password != demoPassword) {
      throw const AuthFailure('E-mail ou senha incorretos.');
    }
    _user = demoUser;
    _controller.add(_user);
  }

  @override
  Future<void> signOut() async {
    _user = null;
    _controller.add(null);
  }

  @override
  Future<String?> readAccessToken() async => _user == null ? null : 'mock-token';

  @override
  Future<String?> refreshAccessToken() async => readAccessToken();
}
