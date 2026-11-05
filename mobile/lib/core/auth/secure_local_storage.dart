import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

/// Persiste a sessão do Supabase no **Keychain (iOS) / Keystore (Android)**.
///
/// O padrão do `supabase_flutter` grava em `shared_preferences` — texto puro no disco, legível
/// em backup ou aparelho com root. O refresh token dá acesso a dados protegidos por sigilo
/// profissional, então fica no armazenamento seguro do sistema.
class SecureLocalStorage extends LocalStorage {
  const SecureLocalStorage([this._storage = const FlutterSecureStorage()]);

  final FlutterSecureStorage _storage;

  @override
  Future<void> initialize() async {}

  @override
  Future<bool> hasAccessToken() async =>
      (await _storage.containsKey(key: supabasePersistSessionKey));

  @override
  Future<String?> accessToken() => _storage.read(key: supabasePersistSessionKey);

  @override
  Future<void> removePersistedSession() => _storage.delete(key: supabasePersistSessionKey);

  @override
  Future<void> persistSession(String persistSessionString) =>
      _storage.write(key: supabasePersistSessionKey, value: persistSessionString);
}
