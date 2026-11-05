import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';
import 'auth_repository.dart';
import 'session_user.dart';

/// Estado de autenticação do app.
///
/// Há dois conceitos distintos, e confundi-los abre uma brecha:
/// - **sessão**: o Supabase restaurou um token do armazenamento seguro ([user] != null);
/// - **desbloqueio**: o usuário provou que é quem está com o aparelho agora ([locked] == false).
///
/// Com o bloqueio biométrico ligado, abrir o app com sessão restaurada NÃO libera o conteúdo:
/// [locked] começa verdadeiro e só a biometria (ou uma nova senha) o desliga.
class SessionState {
  const SessionState({this.user, this.locked = false});

  final SessionUser? user;
  final bool locked;

  bool get hasSession => user != null;

  /// Pode ver o conteúdo do app.
  bool get isSignedIn => hasSession && !locked;

  SessionState copyWith({SessionUser? user, bool? locked, bool clearUser = false}) => SessionState(
        user: clearUser ? null : (user ?? this.user),
        locked: locked ?? this.locked,
      );
}

class AuthController extends Notifier<SessionState> {
  StreamSubscription<SessionUser?>? _subscription;

  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  SessionState build() {
    final repository = ref.watch(authRepositoryProvider);
    final lockEnabled = ref.read(settingsStoreProvider).biometricLock;

    _subscription = repository.authChanges.listen(_onAuthChanged);
    ref.onDispose(() => _subscription?.cancel());

    final user = repository.currentUser;
    return SessionState(user: user, locked: user != null && lockEnabled);
  }

  void _onAuthChanged(SessionUser? user) {
    // Logout ou sessão expirada: esquece tudo. Login novo: já entrou com a senha, não precisa
    // de biometria em seguida. Renovação de token (mesmo usuário): mantém o estado atual.
    if (user == null) {
      state = const SessionState();
    } else if (state.user?.id != user.id) {
      state = SessionState(user: user);
    } else {
      state = state.copyWith(user: user);
    }
  }

  Future<void> signIn({required String email, required String password}) async {
    await _repository.signIn(email: email, password: password);
    final user = _repository.currentUser;
    if (user != null) state = SessionState(user: user);
  }

  /// Desbloqueia com a biometria. `false` se o usuário cancelou ou falhou.
  Future<bool> unlockWithBiometrics() async {
    if (!state.hasSession) return false;

    final ok = await ref
        .read(biometricServiceProvider)
        .authenticate('Confirme sua identidade para abrir o app');
    if (ok) state = state.copyWith(locked: false);
    return ok;
  }

  /// Liga/desliga o bloqueio. Para LIGAR exige uma biometria agora: evita ativar um bloqueio
  /// que depois ninguém consegue passar (aparelho sem biometria cadastrada).
  Future<bool> setBiometricLock(bool enabled) async {
    final settings = ref.read(settingsStoreProvider);
    if (enabled) {
      final biometrics = ref.read(biometricServiceProvider);
      if (!await biometrics.isAvailable()) return false;
      if (!await biometrics.authenticate('Confirme para ativar o bloqueio por biometria')) {
        return false;
      }
    }
    await settings.setBiometricLock(enabled);
    ref.invalidate(biometricLockProvider);
    return true;
  }

  Future<void> signOut() async {
    await _repository.signOut();
    state = const SessionState();
  }

  /// Chamado pelo cliente HTTP quando a sessão acabou de verdade (401 após renovação).
  void sessionExpired() {
    if (state.hasSession) state = const SessionState();
  }
}
